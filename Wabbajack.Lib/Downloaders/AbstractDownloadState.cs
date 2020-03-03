using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public interface IMetaState
    {
        string URL { get; }
        string Name { get; }
        string Author { get; }
        string Version { get;}
        string ImageURL { get; }
        bool IsNSFW { get; }
        string Description { get; }
    }

    public abstract class AbstractDownloadState
    {

        public static List<Type> KnownSubTypes = new List<Type>()
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
            typeof(BethesdaNetDownloader.State)
        };
        public static Dictionary<string, Type> NameToType { get; set; }
        public static Dictionary<Type, string> TypeToName { get; set; }

        static AbstractDownloadState()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

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
        public abstract Task<bool> Download(Archive a, string destination);

        public async Task<bool> Download(string destination)
        {
            var path = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            return await Download(new Archive {Name = Path.GetFileName(destination)}, destination);
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
