using System.Text.Json.Serialization;

namespace Wabbajack.Configuration;

public class MainSettings
{
    public const string SettingsFileName = "app_settings";

    [JsonPropertyName("CurrentSettingsVersion")]
    public int CurrentSettingsVersion { get; set; }

    public int MaximumMemoryPerDownloadThreadInMB
    {
        get => Performance.MaximumMemoryPerDownloadThreadMb;
        set => Performance.MaximumMemoryPerDownloadThreadMb = value;
    }

    public long MinimumFileSizeForResumableDownloadMB {
        get => Performance.MinimumFileSizeForResumableDownload;
        set => Performance.MinimumFileSizeForResumableDownload = value;
    }

    private const int SettingsVersion = 1;

    [JsonInclude]
    [JsonPropertyName("PerformanceSettings")]
    private PerformanceSettings Performance { get; set; } = new();


    public bool Upgrade()
    {
        if (CurrentSettingsVersion == SettingsVersion)
        {
            return false;
        }

        if (CurrentSettingsVersion < 1)
        {
            Performance.MaximumMemoryPerDownloadThreadMb = -1;
        }

        CurrentSettingsVersion = SettingsVersion;
        return true;
    }

    internal class PerformanceSettings
    {
        public int MaximumMemoryPerDownloadThreadMb { get; set; } = -1;
        public long MinimumFileSizeForResumableDownload { get; set; } = -1;
    }
}