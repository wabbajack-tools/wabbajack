using System;
using System.Text.Json.Serialization;

namespace Wabbajack.Networking.NexusApi.DTOs;

public class ModFile
{
    [JsonPropertyName("id")] public int[] Id { get; set; } = Array.Empty<int>();

    [JsonPropertyName("uid")] public object Uid { get; set; } = "";

    [JsonPropertyName("file_id")] public int FileId { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("version")] public string Version { get; set; } = "";

    [JsonPropertyName("category_id")] public int CategoryId { get; set; }

    [JsonPropertyName("category_name")] public string? CategoryName { get; set; } = null;

    [JsonPropertyName("is_primary")] public bool IsPrimary { get; set; }

    [JsonPropertyName("size")] public int Size { get; set; }

    [JsonPropertyName("file_name")] public string FileName { get; set; } = "";

    [JsonPropertyName("uploaded_timestamp")]
    public int UploadedTimestamp { get; set; }

    [JsonPropertyName("uploaded_time")] public DateTime UploadedTime { get; set; }

    [JsonPropertyName("mod_version")] public string ModVersion { get; set; } = "";

    [JsonPropertyName("external_virus_scan_url")]
    public string ExternalVirusScanUrl { get; set; } = "";

    [JsonPropertyName("description")] public string Description { get; set; } = "";

    [JsonPropertyName("size_kb")] public int SizeKb { get; set; }

    [JsonPropertyName("size_in_bytes")] public long? SizeInBytes { get; set; }

    [JsonPropertyName("changelog_html")] public string ChangelogHtml { get; set; } = "";

    [JsonPropertyName("content_preview_link")]
    public string ContentPreviewLink { get; set; } = "";
}

public class ModFiles
{
    [JsonPropertyName("files")] public ModFile[] Files { get; set; } = Array.Empty<ModFile>();
}
