using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Util;

namespace Wabbajack;

[JsonName("Mo2ModListInstallerSettings")]
public class Mo2ModlistInstallationSettings
{
    public AbsolutePath InstallationLocation { get; set; }
    public AbsolutePath DownloadLocation { get; set; }
    public bool AutomaticallyOverrideExistingInstall { get; set; }
}

public class PerformanceSettings : ViewModel
{
    private readonly Configuration.MainSettings _settings;

    public PerformanceSettings(Configuration.MainSettings settings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams)
    {
        var p = systemParams.Create();

        _settings = settings;
    }

}
public class GalleryFilterSettings
{
    public string GameType { get; set; }
    public bool IncludeNSFW { get; set; }
    public bool IncludeUnofficial { get; set; }
    public bool OnlyInstalled { get; set; }
    public string Search { get; set; }
}
