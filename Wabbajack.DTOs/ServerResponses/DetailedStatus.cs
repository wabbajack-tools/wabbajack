using System;

namespace Wabbajack.DTOs.ServerResponses;

public class DetailedStatus
{
    public string Name { get; set; }
    public DateTime Checked { get; set; } = DateTime.UtcNow;
    public DetailedStatusItem[] Archives { get; set; } = Array.Empty<DetailedStatusItem>();
    public DownloadMetadata DownloadMetaData { get; set; }
    public bool HasFailures { get; set; }
    public string MachineName { get; set; }
}