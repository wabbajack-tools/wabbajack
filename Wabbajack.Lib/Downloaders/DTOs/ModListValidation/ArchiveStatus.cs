using System.Text.Json.Serialization;

namespace Wabbajack.Lib.Downloaders.DTOs.ModListValidation
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ArchiveStatus
    {
        Valid,
        InValid,
        Updating,
        Updated,
        Mirrored
    }
}
