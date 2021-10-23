using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins;

public class NexusApiState
{
    [JsonPropertyName("api-key")] public string ApiKey { get; set; }

    [JsonPropertyName("cookies")] public Cookie[] Cookies { get; set; }
}