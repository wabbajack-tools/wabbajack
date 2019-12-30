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
        public static List<Type> KnowSubtypes = new List<Type>();

        [BsonIgnore]
        public abstract string Description { get; }
    }
}
