namespace Wabbajack.Configuration;

public class PerformanceSettings
{
    public int MaximumMemoryPerDownloadThreadMb { get; set; }

    public long MinimumFileSizeForResumableDownload { get; set; } = (long)1024 * 1024 * 500; // 500MB
}