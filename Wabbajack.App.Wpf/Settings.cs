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

        public PerformanceSettings(Configuration.MainSettings settings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams)
        {
            var p = systemParams.Create();

            _settings = settings;
            // Split half of available memory among download threads
            _defaultMaximumMemoryPerDownloadThreadMb = (int)(p.SystemMemorySize / downloadResources.MaxTasks / 1024 / 1024) / 2;
            _maximumMemoryPerDownloadThreadMb = settings.PerformanceSettings.MaximumMemoryPerDownloadThreadMb;

            if (MaximumMemoryPerDownloadThreadMb < 0)
            {
                ResetMaximumMemoryPerDownloadThreadMb();
            }
        }

        private int _maximumMemoryPerDownloadThreadMb;

        public int MaximumMemoryPerDownloadThreadMb
        {
            get => _maximumMemoryPerDownloadThreadMb;
            set
            {
                RaiseAndSetIfChanged(ref _maximumMemoryPerDownloadThreadMb, value);
                _settings.PerformanceSettings.MaximumMemoryPerDownloadThreadMb = value;
            }
        }

        public void ResetMaximumMemoryPerDownloadThreadMb()
        {
            MaximumMemoryPerDownloadThreadMb = _defaultMaximumMemoryPerDownloadThreadMb;
        }
    }
}
