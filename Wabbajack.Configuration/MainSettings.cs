using System.Text.Json.Serialization;

namespace Wabbajack.Configuration;

public class MainSettings
{
    public const string SettingsFileName = "app_settings";

    [JsonPropertyName("CurrentSettingsVersion")]
    public int CurrentSettingsVersion { get; set; }

    public int MaximumMemoryPerDownloadThreadInMB
    {
        get => Performance.MaximumMemoryPerDownloadThreadMB;
        set => Performance.MaximumMemoryPerDownloadThreadMB = value;
    }

    public long MinimumFileSizeForResumableDownloadMB {
        get => Performance.MinimumFileSizeForResumableDownloadMB;
        set => Performance.MinimumFileSizeForResumableDownloadMB = value;
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
            Performance.MaximumMemoryPerDownloadThreadMB = -1;
        }

        CurrentSettingsVersion = SettingsVersion;
        return true;
    }

    internal class PerformanceSettings
    {
        public int MaximumMemoryPerDownloadThreadMB { get; set; } = -1;
        public long MinimumFileSizeForResumableDownloadMB { get; set; } = -1;
    }
}