using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.Networking.Steam.DTOs;


public class AppInfo
{

    [JsonPropertyName("depots")] 
    public Dictionary<string, Depot> Depots { get; set; } = new();

}

public class Depot
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("config")]
    public DepotConfig Config { get; set; }
    
    [JsonPropertyName("maxsize")]
    public ulong MaxSize { get; set; }
    
    [JsonPropertyName("depotfromapp")]
    public uint DepotFromApp { get; set; }
    
    [JsonPropertyName("sharedinstall")]
    public uint SharedInstall { get; set; }

    [JsonPropertyName("manifests")] 
    public Dictionary<string, string> Manifests { get; set; } = new();
}

public class DepotConfig
{
    [JsonPropertyName("oslist")]
    public string OSList { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }
}