using System.Text.Json.Serialization;

namespace Wabbajack.Server.DTOs;

public class UpdateEntry
{
    [JsonPropertyName("mod_id")] public long ModId { get; set; }

    [JsonPropertyName("latest_file_update")]
    public long LatestFileUpdate { get; set; }

    [JsonPropertyName("latest_mod_activity")]
    public long LastestModActivity { get; set; }
}