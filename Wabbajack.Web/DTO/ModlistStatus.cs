using System;
using System.Text.Json.Serialization;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace Wabbajack.Web.DTO
{
    public class ModlistStatus
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("machineURL")]
        public string MachineUrl { get; set; }

        [JsonPropertyName("checked")]
        public DateTime Checked { get; set; }

        [JsonPropertyName("failed")]
        public int Failed { get; set; }

        [JsonPropertyName("passed")]
        public int Passed { get; set; }

        [JsonPropertyName("updating")]
        public int Updating { get; set; }

        [JsonPropertyName("mirrored")]
        public int Mirrored { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; }

        [JsonPropertyName("report")]
        public string Report { get; set; }

        [JsonPropertyName("modlist_missing")]
        public bool ModlistMissing { get; set; }

        [JsonPropertyName("has_failures")]
        public bool HasFailures { get; set; }
    }
}
