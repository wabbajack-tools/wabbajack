using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global
#nullable enable

namespace Wabbajack.Web.DTO
{
    public class ModlistMetadata
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("game")]
        public string? Game { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("force_down")]
        public bool ForceDown { get; set; }

        [JsonPropertyName("links")]
        public ModlistLinks? Links { get; set; }

        [JsonPropertyName("download_metadata")]
        public ModlistDownloadMetadata? DownloadMetadata { get; set; }

        [JsonPropertyName("image_contains_title")]
        public bool ImageContainsTitle { get; set; }

        [JsonPropertyName("nsfw")]
        public bool Nsfw { get; set; }

        [JsonPropertyName("utility_list")]
        public bool UtilityList { get; set; }
        
        public class ModlistLinks
        {
            [JsonPropertyName("image")]
            public string? Image { get; set; }

            [JsonPropertyName("readme")]
            public string? Readme { get; set; }

            [JsonPropertyName("download")]
            public string? Download { get; set; }

            [JsonPropertyName("machineURL")]
            public string? MachineUrl { get; set; }

            [JsonPropertyName("changelog")]
            public string? Changelog { get; set; }
        }

        public class ModlistDownloadMetadata
        {
            [JsonPropertyName("Hash")]
            public string? Hash { get; set; }

            [JsonPropertyName("Size")]
            public long Size { get; set; }

            [JsonPropertyName("NumberOfArchives")]
            public long NumberOfArchives { get; set; }

            [JsonPropertyName("SizeOfArchives")]
            public long SizeOfArchives { get; set; }

            [JsonPropertyName("NumberOfInstalledFiles")]
            public long NumberOfInstalledFiles { get; set; }

            [JsonPropertyName("SizeOfInstalledFiles")]
            public long SizeOfInstalledFiles { get; set; }
        }
    }
}
