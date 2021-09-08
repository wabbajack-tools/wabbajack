using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public interface IMetaState
    {
        Uri URL { get; }
        string? Name { get; set; }
        string? Author { get; set; }
        string? Version { get; set; }
        Uri? ImageURL { get; set; }
        bool IsNSFW { get; set; }
        string? Description { get; set; }

        Task<bool> LoadMetaData();
    }

    public abstract class AbstractDownloadState : IUpgradingState
    {
        public static List<Type> KnownSubTypes = new List<Type>
        {
            typeof(DeprecatedLoversLabDownloader.State),
            typeof(DeprecatedVectorPlexusDownloader.State),
            typeof(HTTPDownloader.State),
            typeof(GameFileSourceDownloader.State),
            typeof(GoogleDriveDownloader.State),
            typeof(LoversLabOAuthDownloader.LoversLabState),
            typeof(ManualDownloader.State),
            typeof(MediaFireDownloader.State),
            typeof(MegaDownloader.State),
            typeof(ModDBDownloader.State),
            typeof(NexusDownloader.State),
            typeof(VectorPlexusOAuthDownloader.State),
            typeof(DeadlyStreamDownloader.State),
            typeof(TESAllianceDownloader.State),
            typeof(TESAllDownloader.State),
            typeof(YandexDownloader.State),
            typeof(WabbajackCDNDownloader.State)
        };
        public static Dictionary<string, Type> NameToType { get; set; }
        public static Dictionary<Type, string> TypeToName { get; set; }

        static AbstractDownloadState()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName!.Substring(t.Namespace!.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

        [JsonIgnore]
        public abstract object[] PrimaryKey { get; }
        
        public string PrimaryKeyString
        {
            get
            {
                var pk = new List<object>();
                pk.Add(AbstractDownloadState.TypeToName[GetType()]);
                pk.AddRange(PrimaryKey);
                var pk_str = string.Join("|",pk.Select(p => p.ToString()));
                return pk_str;
            }
        }

        /// <summary>
        /// Returns true if this file is allowed to be downloaded via whitelist
        /// </summary>
        /// <param name="whitelist"></param>
        /// <returns></returns>
        public abstract bool IsWhitelisted(ServerWhitelist whitelist);

        /// <summary>
        /// Downloads this file to the given destination location
        /// </summary>
        /// <param name="destination"></param>
        public abstract Task<bool> Download(Archive a, AbsolutePath destination);

        public async Task<bool> Download(AbsolutePath destination)
        {
            destination.Parent.CreateDirectory();
            return await Download(new Archive(this) {Name = (string)destination.FileName}, destination);
        }

        /// <summary>
        /// Returns true if this link is still valid
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> Verify(Archive archive, CancellationToken? token = null);

        public abstract IDownloader GetDownloader();

        public abstract string? GetManifestURL(Archive a);
        public abstract string[] GetMetaIni();

        public string GetMetaIniString()
        {
            return string.Join("\n", GetMetaIni());
        }

        public static async Task<(Archive? Archive, TempFile NewFile)> ServerFindUpgrade(Archive a)
        {
            var alternatives = await ClientAPI.GetModUpgrades(a.Hash);
            if (alternatives == default)
                return default;


            await DownloadDispatcher.PrepareAll(alternatives.Select(r => r.State));
            Archive? selected = null;
            foreach (var result in alternatives)
            {
                try
                {
                    if (!await result.State.Verify(result)) continue;

                    selected = result;
                    break;
                }
                catch (Exception ex)
                {
                    Utils.Log($"Verification error for failed for possible upgrade {result.State.PrimaryKeyString}");
                    Utils.Log(ex.ToString());
                }
            }

            if (selected == null) return default;

            var tmpFile = new TempFile();
            if (await selected.State.Download(selected, tmpFile.Path))
            {
                return (selected, tmpFile);
            }

            await tmpFile.DisposeAsync();
            return default;

        }

        public virtual async Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver)
        {
            return await ServerFindUpgrade(a);
        }

        public virtual async Task<bool> ServerValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState)
        {
            var alternatives = await ClientAPI.GetModUpgrades(srcHash);
            return alternatives?.Any(a => a.State.PrimaryKeyString == newArchiveState.PrimaryKeyString) ?? default;
        }

        public virtual async Task<bool> ValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState)
        {
            return await ServerValidateUpgrade(srcHash, newArchiveState);
        }

        internal static async Task<bool> TrySave(HttpResponseMessage streamResult, Archive archive, AbsolutePath path, bool quickMode = false)
        {
            long headerContentSize = streamResult.Content.Headers.ContentLength ?? 0;

            if (archive.Size != 0 && headerContentSize != 0 && archive.Size != headerContentSize)
            {
                Utils.Log($"Bad Header Content sizes {archive.Size} vs {headerContentSize}");
                return false;
            }

            if (quickMode)
            {
                streamResult.Dispose();
                return true;
            }

            await using (var fileStream = await path.Create())
            await using (var contentStream = await streamResult.Content.ReadAsStreamAsync())
            {
                if (archive.Size == 0)
                {
                    Utils.Status($"Downloading {archive.Name}");
                    await contentStream.CopyToAsync(fileStream);
                }
                else
                {
                    await contentStream.CopyToWithStatusAsync(headerContentSize, fileStream, $"Downloading {archive.Name}");
                }
            }

            streamResult.Dispose();

            return true;
        }
    }
}
