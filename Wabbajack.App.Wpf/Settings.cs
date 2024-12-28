using SteamKit2.GC.Dota.Internal;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Util;

namespace Wabbajack
{
    [JsonName("Mo2ModListInstallerSettings")]
    public class Mo2ModlistInstallationSettings
    {
        public AbsolutePath InstallationLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        public bool AutomaticallyOverrideExistingInstall { get; set; }
    }

    public class PerformanceSettingsViewModel : ViewModel
    {
        private readonly Configuration.MainSettings _settings;
        private readonly int _defaultMaximumMemoryPerDownloadThreadMB;
        private readonly long _defaultMinimumFileSizeForResumableDownloadMB;

        public PerformanceSettingsViewModel(Configuration.MainSettings settings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams)
        {
            var p = systemParams.Create();

            _settings = settings;
            // Split half of available memory among download threads
            _defaultMaximumMemoryPerDownloadThreadMB = (int)(p.SystemMemorySize / downloadResources.MaxTasks / 1024 / 1024) / 2;
            _defaultMinimumFileSizeForResumableDownloadMB = long.MaxValue;
            _maximumMemoryPerDownloadThreadMB = settings.MaximumMemoryPerDownloadThreadInMB;
            _minimumFileSizeForResumableDownloadMB = settings.MinimumFileSizeForResumableDownloadMB;

            if (MaximumMemoryPerDownloadThreadMb < 0)
            {
                ResetMaximumMemoryPerDownloadThreadMb();
            }

            if (settings.MinimumFileSizeForResumableDownloadMB < 0)
            {
                ResetMinimumFileSizeForResumableDownload();
            }
        }

        private int _maximumMemoryPerDownloadThreadMB;
        private long _minimumFileSizeForResumableDownloadMB;

        public int MaximumMemoryPerDownloadThreadMb
        {
            get => _maximumMemoryPerDownloadThreadMB;
            set
            {
                RaiseAndSetIfChanged(ref _maximumMemoryPerDownloadThreadMB, value);
                _settings.MaximumMemoryPerDownloadThreadInMB = value;
            }
        }

        public long MinimumFileSizeForResumableDownload
        {
            get => _minimumFileSizeForResumableDownloadMB;
            set
            {
                RaiseAndSetIfChanged(ref _minimumFileSizeForResumableDownloadMB, value);
                _settings.MinimumFileSizeForResumableDownloadMB = value;
            }
        }

        public void ResetMaximumMemoryPerDownloadThreadMb()
        {
            MaximumMemoryPerDownloadThreadMb = _defaultMaximumMemoryPerDownloadThreadMB;
        }

        public void ResetMinimumFileSizeForResumableDownload()
        {
            MinimumFileSizeForResumableDownload = _defaultMinimumFileSizeForResumableDownloadMB;
        }
    }
}
