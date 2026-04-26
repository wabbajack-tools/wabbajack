using System;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs;

public class ModListSummary
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("machineURL")] public string MachineURL { get; set; } = string.Empty;

    [JsonPropertyName("failed")] public int Failed { get; set; }

    [JsonPropertyName("passed")] public int Passed { get; set; }

    [JsonPropertyName("updating")] public int Updating { get; set; }

    [JsonPropertyName("mirrored")] public int Mirrored { get; set; }

    [JsonPropertyName("link")] public string Link => $"reports/{MachineURL}/status.json";

    [JsonPropertyName("report")] public string Report => $"reports/{MachineURL}/status.md";

    [JsonPropertyName("modlist_missing")] public bool ModListIsMissing { get; set; }

    [JsonPropertyName("has_failures")] public bool HasFailures => Failed > 0 || ModListIsMissing;
    
    [JsonPropertyName("small_image")] public Uri SmallImage { get; set; }
    [JsonPropertyName("large_image")] public Uri LargeImage { get; set; }
}