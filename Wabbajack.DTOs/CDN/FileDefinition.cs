using System;
using System.Text.Json.Serialization;
using System.Web;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.CDN;

public class FileDefinition
{
    public string? Author { get; set; }
    public RelativePath OriginalFileName { get; set; }
    public long Size { get; set; }
    public Hash Hash { get; set; }
    public PartDefinition[] Parts { get; set; } = { };
    public string? ServerAssignedUniqueId { get; set; }
    public string MungedName => $"{OriginalFileName}_{ServerAssignedUniqueId!}";

}