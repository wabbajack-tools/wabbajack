using Newtonsoft.Json;

namespace Wabbajack.BuildServer.Models
{
    public class NexusUpdateEntry
    {
        [JsonProperty("mod_id")]
        public long ModId { get; set; }
        [JsonProperty("latest_file_update")]
        public long LatestFileUpdate { get; set; }
        [JsonProperty("latest_mod_activity")]
        public long LastestModActivity { get; set; }
    }
}
