using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    /// <summary>
    /// Base for all abstract downloaders
    /// </summary>
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(HTTPDownloader.State), typeof(GameFileSourceDownloader.State), typeof(GoogleDriveDownloader.State),
    typeof(LoversLabDownloader.State), typeof(ManualDownloader.State), typeof(MediaFireDownloader.State), typeof(MegaDownloader.State),
    typeof(ModDBDownloader.State), typeof(NexusDownloader.State), typeof(SteamWorkshopDownloader.State))]
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
            typeof(SteamWorkshopDownloader.State)
        };
        public static Dictionary<string, Type> NameToType { get; set; }
        public static Dictionary<Type, string> TypeToName { get; set; }

        static AbstractDownloadState()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
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
        public abstract Task Download(Archive a, string destination);

        public async Task Download(string destination)
        {
            await Download(new Archive {Name = Path.GetFileName(destination)}, destination);
        }

        /// <summary>
        /// Returns true if this link is still valid
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> Verify();

        public abstract IDownloader GetDownloader();

        public abstract string GetReportEntry(Archive a);
    }
}
