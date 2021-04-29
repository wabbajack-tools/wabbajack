using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    [JsonName("DetailedStatus")]
    public class DetailedStatus
    {
        public string Name { get; set; } = "";
        public DateTime Checked { get; set; } = DateTime.UtcNow;
        public List<DetailedStatusItem> Archives { get; set; } = new();
        public DownloadMetadata DownloadMetaData { get; set; } = new();
        public bool HasFailures { get; set; }
        public string MachineName { get; set; } = "";
    }

    [JsonName("DetailedStatusItem")]
    public class DetailedStatusItem
    {
        public bool IsFailing { get; set; }
        public Archive Archive { get; set; } 

        public string Name => string.IsNullOrWhiteSpace(Archive!.Name) ? Archive.State.PrimaryKeyString : Archive.Name;
        public string Url => Archive?.State.GetManifestURL(Archive!);

        [JsonIgnore]
        public bool HasUrl => Url != null;
        public ArchiveStatus ArchiveStatus { get; set; }
    }
    
    public enum ArchiveStatus
    {
        Valid,
        InValid,
        Updating,
        Updated,
        Mirrored
    }
    
    public class ClientAPIEx
    {
        public static async Task<DetailedStatus> GetDetailedStatus(string machineURL)
        {
            var client = await ClientAPI.GetClient();
            var results =
                await client.GetJsonAsync<DetailedStatus>(
                    $"{Consts.WabbajackBuildServerUri}lists/status/{machineURL}.json");
            return results;
        }
    }
}
