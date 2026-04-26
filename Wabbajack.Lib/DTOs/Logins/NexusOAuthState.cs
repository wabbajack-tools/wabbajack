using System.Text.Json.Serialization;
using Wabbajack.DTOs.OAuth;

namespace Wabbajack.DTOs.Logins;

public class NexusOAuthState
{
    [JsonPropertyName("oauth")] 
    public JwtTokenReply? OAuth { get; set; } = new();
    
    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;
}