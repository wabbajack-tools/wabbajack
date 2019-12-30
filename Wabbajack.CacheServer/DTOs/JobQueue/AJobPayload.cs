using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace Wabbajack.CacheServer.DTOs.JobQueue
{
    public abstract class AJobPayload
    {
        public static List<Type> KnownSubTypes = new List<Type> {typeof(IndexJob)};
        public static Dictionary<Type, string> TypeToName { get; set; }
        public static Dictionary<string, Type> NameToType { get; set; }


        [BsonIgnore]
        public abstract string Description { get; }

        public virtual bool UsesNexus { get; } = false;

        public abstract JobResult Execute();

        static AJobPayload()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

    }
}
