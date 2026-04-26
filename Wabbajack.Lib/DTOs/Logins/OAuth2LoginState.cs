using System;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins;

public abstract class OAuth2LoginState
{
    [JsonPropertyName("result_state")] public OAuthResultState ResultState { get; set; } = new();

    [JsonPropertyName("cookies")] public Cookie[] Cookies { get; set; } = Array.Empty<Cookie>();

    [JsonIgnore] public abstract string SiteName { get; }
    [JsonIgnore] public abstract string[] Scopes { get; }

    [JsonIgnore] public abstract string ClientID { get; }

    [JsonIgnore] public abstract Uri AuthorizationEndpoint { get; }

    [JsonIgnore] public abstract Uri TokenEndpoint { get; }
}

public class OAuthResultState
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";

    [JsonPropertyName("expires_in")] public long ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";

    [JsonPropertyName("scope")] public string Scope { get; set; } = "";

    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; set; } = "";

    [JsonPropertyName("token_endpoint")] public Uri? TokenEndpoint { get; set; }

    [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("client_id")] public string ClientID { get; set; } = "";
}