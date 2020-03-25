using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MessagePack;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public interface IMetaState
    {
        Uri URL { get; }
        string Name { get; set; }
        string Author { get; set; }
        string Version { get; set; }
        string ImageURL { get; set; }
        bool IsNSFW { get; set; }
        string Description { get; set; }

        Task<bool> LoadMetaData();
    }

    [MessagePackObject]
    [Union(0, typeof(HTTPDownloader.State))]
    [Union(1, typeof(GameFileSourceDownloader.State))]
    [Union(2, typeof(GoogleDriveDownloader.State))]
    [Union(3, typeof(LoversLabDownloader.State))]
    [Union(4, typeof(ManualDownloader.State))]
    [Union(5, typeof(MediaFireDownloader.State))]
    [Union(6, typeof(MegaDownloader.State))]
    [Union(7, typeof(ModDBDownloader.State))]
    [Union(8, typeof(NexusDownloader.State))]
    [Union(9, typeof(SteamWorkshopDownloader.State))]
    [Union(10, typeof(VectorPlexusDownloader.State))]
    [Union(11, typeof(AFKModsDownloader.State))]
    [Union(12, typeof(TESAllianceDownloader.State))]
    [Union(13, typeof(BethesdaNetDownloader.State))]
    [Union(14, typeof(YouTubeDownloader.State))]
    public abstract class AbstractDownloadState
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
            typeof(AFKModsDownloader.State),
            typeof(TESAllianceDownloader.State),
            typeof(BethesdaNetDownloader.State),
            typeof(YouTubeDownloader.State)
        };
        public static Dictionary<string, Type> NameToType { get; set; }
        public static Dictionary<Type, string> TypeToName { get; set; }

        static AbstractDownloadState()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

        [IgnoreMember]
        public abstract object[] PrimaryKey { get; }
        
        [IgnoreMember]
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
            return await Download(new Archive {Name = (string)destination.FileName}, destination);
        }

        /// <summary>
        /// Returns true if this link is still valid
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> Verify(Archive archive);

        public abstract IDownloader GetDownloader();

        public abstract string GetManifestURL(Archive a);
        public abstract string[] GetMetaIni();
    }
}
