using System;

namespace Wabbajack.DTOs.GitHub;

public class UpdateRequest
{
    public string MachineUrl { get; set; } = "";
    public Version? Version { get; set; } = new();
    public Uri DownloadUrl { get; set; } = new("https://www.wabbajack.org");
    public DownloadMetadata DownloadMetadata { get; set; } = new();
}