using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins.BethesdaNet;

public class BeamLogin
{
    [JsonPropertyName("password")]
    public string Password { get; set; }
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}