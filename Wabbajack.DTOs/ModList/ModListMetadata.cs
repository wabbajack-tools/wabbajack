using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs
{
    [JsonName("ModListMetadata, Wabbajack.Lib")]
    [JsonAlias("ModListMetadata")]
    public class ModlistMetadata
    {
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

        [JsonPropertyName("author")] public string Author { get; set; } = string.Empty;

        [JsonPropertyName("maintainers")] public string[] Maintainers { get; set; } = Array.Empty<string>();

        [JsonPropertyName("game")] public Game Game { get; set; }

        [JsonPropertyName("official")] public bool Official { get; set; }

        [JsonPropertyName("tags")] public List<string> tags { get; set; } = new();

        [JsonPropertyName("nsfw")] public bool NSFW { get; set; }

        [JsonPropertyName("utility_list")] public bool UtilityList { get; set; }

        [JsonPropertyName("image_contains_title")]
        public bool ImageContainsTitle { get; set; }

        [JsonPropertyName("force_down")] public bool ForceDown { get; set; }

        [JsonPropertyName("links")] public LinksObject Links { get; set; } = new();

        [JsonPropertyName("download_metadata")]
        public DownloadMetadata? DownloadMetadata { get; set; }

        [JsonPropertyName("version")] public Version? Version { get; set; }

        [JsonIgnore] public ModListSummary ValidationSummary { get; set; } = new();
    }
}