using System.Text.Json.Serialization;

namespace Wabbajack.Networking.BethesdaNet.DTOs;


public class BuildHistory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}

public class BuildFields
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("build_type")]
    public int BuildType { get; set; }

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("storage_key")]
    public string StorageKey { get; set; }

    [JsonPropertyName("major")]
    public bool Major { get; set; }
}

public class Chunk
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; set; }

    [JsonPropertyName("uncompressed_size")]
    public int UncompressedSize { get; set; }

    [JsonPropertyName("sha")]
    public string Sha { get; set; }
}

public class FileList
{
    [JsonPropertyName("file_id")]
    public int FileId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("sha")]
    public string Sha { get; set; }

    [JsonPropertyName("file_size")]
    public int FileSize { get; set; }

    [JsonPropertyName("compressed_size")]
    public int CompressedSize { get; set; }

    [JsonPropertyName("chunk_count")]
    public int ChunkCount { get; set; }

    [JsonPropertyName("modifiable")]
    public bool Modifiable { get; set; }

    [JsonPropertyName("chunk_list")]
    public Chunk[] ChunkList { get; set; }
}

public class DepotList
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

    [JsonPropertyName("is_dlc")]
    public bool IsDlc { get; set; }

    [JsonPropertyName("file_list")]
    public FileList[] FileList { get; set; }
}

public class Tree
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("entitlement_id")]
    public int EntitlementId { get; set; }

    [JsonPropertyName("branch_type")]
    public int BranchType { get; set; }

    [JsonPropertyName("project")]
    public int Project { get; set; }

    [JsonPropertyName("build")]
    public int Build { get; set; }
    
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("preload")]
    public bool Preload { get; set; }

    [JsonPropertyName("preload_ondeck")]
    public bool PreloadOndeck { get; set; }
    
    [JsonPropertyName("diff_type")]
    public int DiffType { get; set; }

    [JsonPropertyName("build_history_length")]
    public int BuildHistoryLength { get; set; }
    
    [JsonPropertyName("promote_ondeck_after_diff")]
    public bool PromoteOndeckAfterDiff { get; set; }

    [JsonPropertyName("storage_url")]
    public string StorageUrl { get; set; }
    
    [JsonPropertyName("build_history")]
    public List<BuildHistory> BuildHistory { get; set; }

    [JsonPropertyName("build_fields")]
    public BuildFields BuildFields { get; set; }

    [JsonPropertyName("depot_list")]
    public List<DepotList> DepotList { get; set; }
}

