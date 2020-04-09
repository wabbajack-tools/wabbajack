using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Models
{
    public class ModListStatus
    {
        public string Id { get; set; }
        public ModListSummary Summary { get; set; }

        public ModlistMetadata Metadata { get; set; }
        public DetailedStatus DetailedStatus { get; set; }

        public static IQueryable<ModListStatus> AllSummaries
        {
            get
            {
                return null;
            }
        }
    }

    [JsonName("DetailedStatus")]
    public class DetailedStatus
    {
        public string Name { get; set; }
        public DateTime Checked { get; set; } = DateTime.UtcNow;
        public List<DetailedStatusItem> Archives { get; set; }
        public DownloadMetadata DownloadMetaData { get; set; }
        public bool HasFailures { get; set; }
        public string MachineName { get; set; }
    }

    [JsonName("DetailedStatusItem")]
    public class DetailedStatusItem
    {
        public bool IsFailing { get; set; }
        public Archive Archive { get; set; }
    }
}
