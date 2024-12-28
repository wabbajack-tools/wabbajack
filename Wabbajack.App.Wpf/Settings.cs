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
        private readonly int _defaultMaximumMemoryPerDownloadThreadMb;
        private readonly long _defaultMinimumFileSizeForResumableDownload;

        public PerformanceSettingsViewModel(Configuration.MainSettings settings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams)
        {
            var p = systemParams.Create();

            _settings = settings;
            // Split half of available memory among download threads
            _defaultMaximumMemoryPerDownloadThreadMb = (int)(p.SystemMemorySize / downloadResources.MaxTasks / 1024 / 1024) / 2;
            _defaultMinimumFileSizeForResumableDownload = long.MaxValue;
            _maximumMemoryPerDownloadThreadMb = settings.MaximumMemoryPerDownloadThreadInMB;
            _minimumFileSizeForResumableDownload = settings.MinimumFileSizeForResumableDownloadMB;

            if (MaximumMemoryPerDownloadThreadMb < 0)
            {
                ResetMaximumMemoryPerDownloadThreadMb();
            }

            if (settings.MinimumFileSizeForResumableDownloadMB < 0)
            {
                ResetMinimumFileSizeForResumableDownload();
            }
        }

        private int _maximumMemoryPerDownloadThreadMb;
        private long _minimumFileSizeForResumableDownload;

        public int MaximumMemoryPerDownloadThreadMb
        {
            get => _maximumMemoryPerDownloadThreadMb;
            set
            {
                RaiseAndSetIfChanged(ref _maximumMemoryPerDownloadThreadMb, value);
                _settings.MaximumMemoryPerDownloadThreadInMB = value;
            }
        }

        public long MinimumFileSizeForResumableDownload
        {
            get => _minimumFileSizeForResumableDownload;
            set
            {
                RaiseAndSetIfChanged(ref _minimumFileSizeForResumableDownload, value);
                _settings.MinimumFileSizeForResumableDownloadMB = value;
            }
        }

        public void ResetMaximumMemoryPerDownloadThreadMb()
        {
            MaximumMemoryPerDownloadThreadMb = _defaultMaximumMemoryPerDownloadThreadMb;
        }

        public void ResetMinimumFileSizeForResumableDownload()
        {
            MinimumFileSizeForResumableDownload = _defaultMinimumFileSizeForResumableDownload;
        }
    }
}
