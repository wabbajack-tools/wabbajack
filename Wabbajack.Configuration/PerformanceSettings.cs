namespace Wabbajack.Configuration;

public class PerformanceSettings
{
    public int MaximumMemoryPerDownloadThreadMb { get; set; }

    public long MinimumFileSizeForResumableDownload { get; set; } = 0;
}