using System.Text.Json.Serialization;

namespace Wabbajack.Downloaders.ModDB;

public class MegaToken
{
    [JsonPropertyName("email")]
    public string Email { get; set; }
    
    [JsonPropertyName("password")]
    public string Password { get; set; }
}