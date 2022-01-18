using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Web;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.NexusApi.DTOs;

public enum Category : int
{
    Main = 1,
    Updates = 2,
    Optional = 3,
    Old = 4,
    Misc = 5,
    Archives = 7
}

public enum ChunkStatus
{
    NoContent,
    Waiting,
    Done
}

public class UploadDefinition
{
    public const long ChunkSize = 5242880; // 5MB chunks
    public Game Game { get; set; }
    
    [JsonIgnore]
    public long GameId => Game.MetaData().NexusGameId;

    public string Name { get; set; }
    public AbsolutePath Path { get; set; }
    
    public string Version { get; set; }
    
    public string Category { get; set; }
    
    public bool NewExisting { get; set; }
    
    public long OldFileId { get; set; }
    
    public bool RemoveOldVersion { get; set; }
    
    public string BriefOverview { get; set; }

    public string FileUUID { get; set; } = "";

    public long FileSize => Path.Size();
    public long ModId { get; set; }

    public long TotalChunks => (long) Math.Ceiling(FileSize / (double) ChunkSize);
    public string ResumableIdentifier => FileSize + "-" + Path.FileName.ToString().Replace(".", "").Replace(" ", "");
    public string ResumableRelativePath => HttpUtility.UrlEncode(Path.FileName.ToString());
    public bool SetAsMain { get; set; }

    public IEnumerable<Chunk> Chunks()
    {

        var size = FileSize;

        if (size <= ChunkSize)
        {

            yield return new Chunk
            {
                Index = 0,
                Offset = 0,
                Size = size
            };
            yield break;
        }
        
        for (long block = 0; block * ChunkSize < size; block++) {
            yield return new Chunk
            {
                Index = block,
                Size = Math.Min(ChunkSize, size - block * ChunkSize),
                Offset = block * ChunkSize
            };
        }
    }
}

public class Chunk
{
    public long Index { get; set; }
    public long Size { get; set; }
    public long Offset { get; set; }
}

public class ChunkStatusResult
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    [JsonPropertyName("status")]
    public bool Status { get; set; }
    
    [JsonPropertyName("uuid")]
    public string UUID { get; set; }
}

public class FileStatusResult
{
    [JsonPropertyName("file_chunks_reassembled")]
    public bool FileChunksAssembled { get; set; }
    
    [JsonPropertyName("s3_upload_complete")]
    public bool S3UploadComplete { get; set; }
    
    [JsonPropertyName("virus_total_result")]
    public int VirusTotalStatus { get; set; }

}