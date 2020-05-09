using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
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
            new LoversLabDownloader(),
            new VectorPlexusDownloader(),
            new DeadlyStreamDownloader(),
            new BethesdaNetDownloader(),
            new TESAllianceDownloader(),
            new YouTubeDownloader(),
            new WabbajackCDNDownloader(),
            new HTTPDownloader(),
            new ManualDownloader(),
        };

        public static readonly List<IUrlInferencer> Inferencers = new List<IUrlInferencer>()
        {
            new BethesdaNetInferencer(),
            new YoutubeInferencer(),
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
            return null;

        }

        public static T GetInstance<T>() where T : IDownloader
        {
            var inst = (T)IndexedDownloaders[typeof(T)];
            return inst;
        }

        public static async Task<AbstractDownloadState> ResolveArchive(dynamic ini, bool quickMode = false)
        {
            var states = await Task.WhenAll(Downloaders.Select(d => (Task<AbstractDownloadState>)d.GetDownloaderState(ini, quickMode)));
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

        public static async Task<bool> DownloadWithPossibleUpgrade(Archive archive, AbsolutePath destination)
        {
            var success = await Download(archive, destination);
            if (success)
            {
                await destination.FileHashCachedAsync();
                return true;
            }

            Utils.Log($"Download failed, looking for upgrade");
            var upgrade = await ClientAPI.GetModUpgrade(archive.Hash);
            if (upgrade == null)
            {
                Utils.Log($"No upgrade found for {archive.Hash}");
                return false;
            }
            Utils.Log($"Upgrading via {upgrade.State.PrimaryKeyString}");
            
            Utils.Log($"Upgrading {archive.Hash}");
            var upgradePath = destination.Parent.Combine("_Upgrade_" + archive.Name);
            var upgradeResult = await Download(upgrade, upgradePath);
            if (!upgradeResult) return false;

            var patchName = $"{archive.Hash.ToHex()}_{upgrade.Hash.ToHex()}";
            var patchPath = destination.Parent.Combine("_Patch_" + patchName);

            var patchState = new Archive(new HTTPDownloader.State($"https://wabbajackcdn.b-cdn.net/updates/{patchName}"))
            {
                Name = patchName,
            };

            var patchResult = await Download(patchState, patchPath);
            if (!patchResult) return false;

            Utils.Status($"Applying Upgrade to {archive.Hash}");
            await using (var patchStream = patchPath.OpenRead())
            await using (var srcStream = upgradePath.OpenRead())
            await using (var destStream = destination.Create())
            {
                OctoDiff.Apply(srcStream, patchStream, destStream);
            }

            await destination.FileHashCachedAsync();

            return true;
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
