namespace Wabbajack.Reporting; 
using System.Collections.Generic; 
using System.Text.Json.Serialization; 
using System.Text.RegularExpressions; 
public sealed class TagConfig { [JsonPropertyName("log")] 
    public bool Log { get; set; } [JsonPropertyName("prompt")] 
    public List<string>? Prompt { get; set; } [JsonPropertyName("logprompt")] 
    public List<string>? LogPrompt { get; set; } [JsonPropertyName("text")] 
    public string? Text { get; set; } [JsonPropertyName("image_url")] 
    public string? ImageUrl { get; set; } [JsonPropertyName("image")] 
    public string? Image { get; set; } [JsonIgnore] 
    public string Name { get; set; } = ""; [JsonIgnore] 
    public Regex? Pattern { get; set; } [JsonIgnore] 
    public Regex? LogPattern { get; set; } }