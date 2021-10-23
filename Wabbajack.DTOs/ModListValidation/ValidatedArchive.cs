using System;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.ServerResponses;

namespace Wabbajack.DTOs.ModListValidation;

public class ValidatedArchive
{
    public ArchiveStatus Status { get; set; }
    public Archive Original { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Archive? PatchedFrom { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? PatchUrl { get; set; }
}