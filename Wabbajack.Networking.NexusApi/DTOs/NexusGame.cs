using System.Text.Json.Serialization;

namespace Wabbajack.Networking.NexusApi.DTOs;

/// <summary>One entry of Nexus Mods' game list.</summary>
public class NexusGame
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("domain_name")] public string DomainName { get; set; } = "";
}
