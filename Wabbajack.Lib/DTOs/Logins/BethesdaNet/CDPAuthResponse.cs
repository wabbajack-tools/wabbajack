using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins.BethesdaNet;

public class CDPAuthResponse
{
    [JsonPropertyName("entitlement_ids")]
    public int[] EntitlementIds { get; set; }
    [JsonPropertyName("beam_client_api_key")]
    public string BeamClientApiKey { get; set; }
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; }
    [JsonPropertyName("token")]
    public string Token { get; set; }
    [JsonPropertyName("beam_token")]
    public string[] BeamToken { get; set; }
    [JsonPropertyName("oauth_token")]
    public string OAuthToken { get; set; }
}