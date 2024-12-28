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

    public class PerformanceSettings : ViewModel
    {
        private readonly Configuration.MainSettings _settings;
        private readonly int _defaultMaximumMemoryPerDownloadThreadMb;
        private readonly long _defaultMinimumFileSizeForResumableDownload;

        public PerformanceSettings(Configuration.MainSettings settings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams)
        {
            var p = systemParams.Create();

            _settings = settings;
            // Split half of available memory among download threads
            _defaultMaximumMemoryPerDownloadThreadMb = (int)(p.SystemMemorySize / downloadResources.MaxTasks / 1024 / 1024) / 2;
            _defaultMinimumFileSizeForResumableDownload = long.MaxValue;
            _maximumMemoryPerDownloadThreadMb = settings.PerformanceSettings.MaximumMemoryPerDownloadThreadMb;
            _minimumFileSizeForResumableDownload = settings.PerformanceSettings.MinimumFileSizeForResumableDownload;

            if (MaximumMemoryPerDownloadThreadMb < 0)
            {
                ResetMaximumMemoryPerDownloadThreadMb();
            }

            if (settings.PerformanceSettings.MinimumFileSizeForResumableDownload < 0)
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
                _settings.PerformanceSettings.MaximumMemoryPerDownloadThreadMb = value;
            }
        }

        public long MinimumFileSizeForResumableDownload
        {
            get => _minimumFileSizeForResumableDownload;
            set
            {
                RaiseAndSetIfChanged(ref _minimumFileSizeForResumableDownload, value);
                _settings.PerformanceSettings.MinimumFileSizeForResumableDownload = value;
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
