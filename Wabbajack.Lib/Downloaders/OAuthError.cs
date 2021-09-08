using Newtonsoft.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class OAuthError
    {
        [JsonRequired]
        public string Error { get; set; } = string.Empty;
        
        [JsonProperty("error_description")]
        public string? Description { get; set; }
    }
}
