namespace Wabbajack.Configuration;

public class MainSettings
{
    public const string SettingsFileName = "app_settings";
    private const int SettingsVersion = 1;

    public int CurrentSettingsVersion { get; set; }

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