using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.BuildServer.Models.Jobs;

namespace Wabbajack.BuildServer.Models.JobQueue
{
    public abstract class AJobPayload
    {
        public static List<Type> KnownSubTypes = new List<Type>
        {
            typeof(IndexJob),
            typeof(GetNexusUpdatesJob),
            typeof(UpdateModLists),
            typeof(EnqueueAllArchives),
            typeof(EnqueueAllGameFiles),
            typeof(EnqueueRecentFiles),
            typeof(UploadToCDN)
        };
        public static Dictionary<Type, string> TypeToName { get; set; }
        public static Dictionary<string, Type> NameToType { get; set; }


        [BsonIgnore]
        public abstract string Description { get; }

        public virtual bool UsesNexus { get; } = false;

        public abstract Task<JobResult> Execute(DBContext db, AppSettings settings);

        static AJobPayload()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

    }
}
