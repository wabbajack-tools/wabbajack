using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.ModListValidation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ListStatus
{
    Available,
    Failed,
    ForcedDown
}