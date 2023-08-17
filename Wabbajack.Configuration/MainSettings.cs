using System.Text.Json.Serialization;

namespace Wabbajack.Configuration;

public class MainSettings
{
    private const int SettingsVersion = 1;

    [JsonInclude]
    private int CurrentSettingsVersion { get; set; }

    public PerformanceSettings PerformanceSettings { get; set; } = new();

    public bool Upgrade()
    {
        if (CurrentSettingsVersion == SettingsVersion)
        {
            return false;
        }

        if (CurrentSettingsVersion < 1)
        {
            PerformanceSettings.MaximumMemoryPerDownloadThreadMb = -1;
        }

        CurrentSettingsVersion = SettingsVersion;
        return true;
    }
}