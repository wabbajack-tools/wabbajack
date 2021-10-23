using System;
using System.Text.Json.Serialization;

namespace Wabbajack.Networking.NexusApi.DTOs;

public class DownloadLink
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("short_name")] public string ShortName { get; set; }

    [JsonPropertyName("URI")] public Uri URI { get; set; }
}