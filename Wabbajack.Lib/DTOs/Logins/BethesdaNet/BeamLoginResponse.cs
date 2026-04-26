using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins.BethesdaNet;

public class BeamLoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
    
    [JsonPropertyName("account")]
    public BeamAccount Account { get; set; }
}

public class BeamAccount
{
    [JsonPropertyName("admin")]
    public bool Admin { get; set; }
    
    [JsonPropertyName("admin_read_only")]
    public bool AdminReadOnly { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("mfa_enabled")]
    public bool MFAEnabled { get; set; }
    
    [JsonPropertyName("sms_enabled_number")]
    public object sms_enabled_number { get; set; }
    
    [JsonPropertyName("username")]
    public string UserName { get; set; }
}