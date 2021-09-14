using System.Text.Json.Serialization;

namespace Wabbajack.Lib.Downloaders.DTOs.ModListValidation
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ListStatus : int
    {
        Available,
        Failed,
        ForcedDown
    }
}
