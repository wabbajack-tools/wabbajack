using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
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
            typeof(UploadToCDN),
            typeof(IndexDynDOLOD),
            typeof(ReindexArchives),
            typeof(PatchArchive)
        };
        public static Dictionary<Type, string> TypeToName { get; set; }
        public static Dictionary<string, Type> NameToType { get; set; }


        public abstract string Description { get; }

        public virtual bool UsesNexus { get; } = false;

        public abstract Task<JobResult> Execute(SqlService sql,AppSettings settings);
        
        protected abstract IEnumerable<object> PrimaryKey { get; }

        public string PrimaryKeyString => string.Join("|", PrimaryKey.Cons(this.GetType().Name).Select(i => i.ToString()));

        static AJobPayload()
        {
            NameToType = KnownSubTypes.ToDictionary(t => t.FullName.Substring(t.Namespace.Length + 1), t => t);
            TypeToName = NameToType.ToDictionary(k => k.Value, k => k.Key);
        }

    }
}
