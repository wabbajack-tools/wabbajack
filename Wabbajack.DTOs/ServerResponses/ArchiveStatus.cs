using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.ServerResponses;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArchiveStatus
{
    Valid,
    InValid,
    Updating,
    Updated,
    Mirrored
}