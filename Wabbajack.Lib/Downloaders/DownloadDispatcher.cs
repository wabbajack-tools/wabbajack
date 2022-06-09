using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders.DTOs.ModListValidation;
using Wabbajack.Lib.Downloaders.UrlDownloaders;

namespace Wabbajack.Lib.Downloaders
{
    public static class DownloadDispatcher
    {
        public static readonly List<IDownloader> Downloaders = new List<IDownloader>()
        {
            new GameFileSourceDownloader(),
            new MegaDownloader(),
            new DropboxDownloader(),
            new GoogleDriveDownloader(),
            new ModDBDownloader(),
            new NexusDownloader(),
            new MediaFireDownloader(),
            new LoversLabOAuthDownloader(),
            new VectorPlexusOAuthDownloader(),
            new DeadlyStreamDownloader(),
            new TESAllianceDownloader(),
            new TESAllDownloader(),
            new WabbajackCDNDownloader(),
            new YandexDownloader(),
            new HTTPDownloader(),
            new ManualDownloader(),
            new DeprecatedVectorPlexusDownloader(),
            new DeprecatedLoversLabDownloader(),
        };

        public static readonly List<IUrlInferencer> Inferencers = new List<IUrlInferencer>()
        {
            new WabbajackCDNInfluencer()
        };

        private static readonly Dictionary<Type, IDownloader> IndexedDownloaders;

        static DownloadDispatcher()
        {
            IndexedDownloaders = Downloaders.ToDictionary(d => d.GetType());
        }

        public static async Task<AbstractDownloadState?> Infer(Uri uri)
        {
            foreach (var inf in Inferencers)
            {
                var state = await inf.Infer(uri);
                if (state != null)
                    return state;
            }
            
            var meta = string.Join("\n", new string[]
            {
                "[General]",
                $"directURL={uri}"    
            });
            return (AbstractDownloadState)(await ResolveArchive(meta.LoadIniString()));
        }

        public static T GetInstance<T>() where T : IDownloader
        {
            var inst = (T)IndexedDownloaders[typeof(T)];
            return inst;
        }

        public static async Task<AbstractDownloadState?> ResolveArchive(dynamic ini, bool quickMode = false)
        {
            var states = await Task.WhenAll(Downloaders.Select(d =>
                    (Task<AbstractDownloadState?>)d.GetDownloaderState(ini, quickMode)));
            return states.FirstOrDefault(result => result != null);
        }

        /// <summary>
        /// Reduced version of Resolve archive that requires less information, but only works
        /// with a single URL string
        /// </summary>
        /// <param name="ini"></param>
        /// <returns></returns>
        public static AbstractDownloadState? ResolveArchive(string url)
        {
            return Downloaders.OfType<IUrlDownloader>().Select(d => d.GetDownloaderState(url)).FirstOrDefault(result => result != null);
        }

        public static async Task PrepareAll(IEnumerable<AbstractDownloadState> states)
        {
            await Task.WhenAll(states.Select(s => s.GetDownloader().GetType())
                  .Distinct()
                  .Select(t => Downloaders.First(d => d.GetType() == t).Prepare()));
        }

        public enum DownloadResult
        {
            Failure,
            Update,
            Mirror,
            Success
        }

        public static async Task<DownloadResult> DownloadWithPossibleUpgrade(Archive archive, AbsolutePath destination, ValidatedArchive[]? upgrades = null)
        {
            archive = MaybeProxy(archive);
            
            bool ShouldTry(Archive archive)
            {
                return upgrades == null || upgrades.All(a => a.Original.Hash != archive.Hash);
            }
            
            
            if (ShouldTry(archive) && await Download(archive, destination))
            {
                var downloadedHash = await destination.FileHashCachedAsync();
                if (downloadedHash == archive.Hash || archive.Hash == default) 
                    return DownloadResult.Success;
            }
            

            Utils.Log($"Loading for alternative to {archive.Hash}");
            if (upgrades == null)
            {
                var client = new Http.Client();
                upgrades = (await client.GetJsonAsync<ValidatedArchive[]>(Consts.UpgradedFilesURL));
            }
            
            var replacementMeta = upgrades.FirstOrDefault(a => a.Original.Hash == archive.Hash);

            if (replacementMeta == null)
            {
                Utils.Log($"No alternative for {archive.Hash} could be found");
                return DownloadResult.Failure;
            }
            
            if (replacementMeta.Status == ArchiveStatus.Mirrored && await Download(replacementMeta.PatchedFrom!, destination))
            {
                await destination.FileHashCachedAsync();
                return DownloadResult.Mirror;
            }

            if (replacementMeta.Status != ArchiveStatus.Updated)
            {
                Utils.Log($"Download failed for {archive.Name} and no upgrade from this download source is possible");
                return DownloadResult.Failure;
            }

            Utils.Log($"Downloading patch for {archive.Name}");
            

            await using var tempFile = new TempFile();
            await using var newFile = new TempFile();

            await Download(replacementMeta.PatchedFrom!, newFile.Path);
            
            {
                var client = new Http.Client();
                using var response = await client.GetAsync(replacementMeta.PatchUrl!);
                await using var strm = await response.Content.ReadAsStreamAsync();
                await tempFile.Path.WriteAllAsync(await response.Content.ReadAsStreamAsync());
            }

            Utils.Log($"Applying patch to {archive.Name}");
            await using(var src = await newFile.Path.OpenShared())
            await using (var final = await destination.Create())
            {
                Utils.ApplyPatch(src, () => tempFile.Path.OpenShared().Result, final);
            }

            var hash = await destination.FileHashCachedAsync();
            if (hash != archive.Hash && archive.Hash != default)
            {
                Utils.Log("Archive hash didn't match after patching");
                return DownloadResult.Failure;
            }

            return DownloadResult.Update;
        }

        public static Archive MaybeProxy(Archive archive)
        {
            if (archive.State is (not GoogleDriveDownloader.State 
                and not MegaDownloader.State 
                and not MediaFireDownloader.State 
                and not ModDBDownloader.State
                and not ManualDownloader.State))
                return archive;

            var uri = archive.State.GetManifestURL(archive);
            var hash = archive.Hash != default ? $"&hash={archive.Hash.ToHex()}" : "";
            Utils.Log($"Downloading via proxy ({Encoding.UTF8.GetBytes(uri!).xxHash().ToHex()}) {uri}");
            var newUri = $"https://build.wabbajack.org/proxy?name={archive.Name}{hash}&uri={HttpUtility.UrlEncode(uri)}";

            return new Archive(new HTTPDownloader.State(newUri))
            {
                Name = archive.Name,
                Size = archive.Size,
                Hash = archive.Hash,
            };

        }

        public static async Task<bool> ProxyHas(Uri uri)
        {
            var newUri = $"https://build.wabbajack.org/proxy?uri={HttpUtility.UrlEncode(uri.ToString())}";
            var msg = new HttpRequestMessage(HttpMethod.Head, newUri);
            var client = new Http.Client();
            try
            {
                var result = await client.SendAsync(msg);
                return result.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static async Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>>? downloadResolver = null)
        {
            downloadResolver ??= async a => default;
            return await a.State.FindUpgrade(a, downloadResolver);
        }
        
        private static async Task<bool> Download(Archive archive, AbsolutePath destination)
        {
            try
            {
                var result =  await archive.State.Download(archive, destination);
                if (!result) return false;

                if (!archive.Hash.IsValid) return true;
                var hash = await destination.FileHashCachedAsync();
                if (hash == archive.Hash) return true;

                Utils.Log($"Hashed download is incorrect");
                return false;

            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
