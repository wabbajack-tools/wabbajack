using System.Text.Json.Serialization;

namespace Wabbajack.DTOs;

public class SteamManifest
{
    [JsonPropertyName("Depot")]
    public uint Depot { get; set; }
    
    [JsonPropertyName("Manifest")]
    public ulong Manifest { get; set; }
}