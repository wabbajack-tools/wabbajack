using System;
using System.Collections.Generic;
using System.Linq;
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
            typeof(HTTPDownloader.State),
            typeof(GameFileSourceDownloader.State),
            typeof(GoogleDriveDownloader.State),
            typeof(LoversLabDownloader.State),
            typeof(ManualDownloader.State),
            typeof(MediaFireDownloader.State),
            typeof(MegaDownloader.State),
            typeof(ModDBDownloader.State),
            typeof(NexusDownloader.State),
            typeof(SteamWorkshopDownloader.State),
            typeof(VectorPlexusDownloader.State),
            typeof(DeadlyStreamDownloader.State),
            typeof(TESAllianceDownloader.State),
            typeof(TESAllDownloader.State),
            typeof(BethesdaNetDownloader.State),
            typeof(YouTubeDownloader.State),
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
        public abstract Task<bool> Verify(Archive archive);

        public abstract IDownloader GetDownloader();

        public abstract string? GetManifestURL(Archive a);
        public abstract string[] GetMetaIni();

        public string GetMetaIniString()
        {
            return string.Join("\n", GetMetaIni(), "\n", "installed=true");
        }

        public async Task<(Archive? Archive, TempFile NewFile)> ServerFindUpgrade(Archive a)
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
        

    }
}
