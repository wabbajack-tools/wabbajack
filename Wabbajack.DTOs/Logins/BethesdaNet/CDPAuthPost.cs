using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins.BethesdaNet;

public class CDPAuthPost
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}