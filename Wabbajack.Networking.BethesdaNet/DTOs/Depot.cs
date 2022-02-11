using System.Text.Json.Serialization;

namespace Wabbajack.Networking.BethesdaNet.DTOs;

public class Depot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("properties_id")]
    public int PropertiesId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("build")]
    public int Build { get; set; }

    [JsonPropertyName("bytes_per_chunk")]
    public int BytesPerChunk { get; set; }

    [JsonPropertyName("size_on_disk")]
    public int SizeOnDisk { get; set; }

    [JsonPropertyName("download_size")]
    public int DownloadSize { get; set; }

    [JsonPropertyName("depot_type")]
    public int DepotType { get; set; }

    [JsonPropertyName("deployment_order")]
    public int DeploymentOrder { get; set; }

    [JsonPropertyName("compression_type")]
    public int CompressionType { get; set; }

    [JsonPropertyName("encryption_type")]
    public int EncryptionType { get; set; }

    [JsonPropertyName("language")]
    public int Language { get; set; }

    [JsonPropertyName("region")]
    public int Region { get; set; }

    [JsonPropertyName("default_region")]
    public bool DefaultRegion { get; set; }

    [JsonPropertyName("default_language")]
    public bool DefaultLanguage { get; set; }

    [JsonPropertyName("platform")]
    public int Platform { get; set; }

    [JsonPropertyName("architecture")]
    public int Architecture { get; set; }

    [JsonPropertyName("ex_info_A")]
    public List<byte> ExInfoA { get; set; }

    [JsonPropertyName("ex_info_B")]
    public List<byte> ExInfoB { get; set; }

    [JsonPropertyName("is_dlc")]
    public bool IsDlc { get; set; }
}