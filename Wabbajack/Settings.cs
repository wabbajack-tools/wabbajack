using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;

namespace Wabbajack
{
    [JsonName("MainSettings")]
    [JsonObject(MemberSerialization.OptOut)]
    public class MainSettings
    {
        public byte Version { get; set; } = Consts.SettingsVersion;

        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public InstallerSettings Installer { get; set; } = new InstallerSettings();
        public FiltersSettings Filters { get; set; } = new FiltersSettings();
        public CompilerSettings Compiler { get; set; } = new CompilerSettings();
        public PerformanceSettings Performance { get; set; } = new PerformanceSettings();

        private Subject<Unit> _saveSignal = new Subject<Unit>();
        [JsonIgnore]
        public IObservable<Unit> SaveSignal => _saveSignal;

        public static async ValueTask<(MainSettings settings, bool loaded)> TryLoadTypicalSettings()
        {
            if (!Consts.SettingsFile.Exists)
            {
                return default;
            }

            // Version check
            try
            {
                var settings = Consts.SettingsFile.FromJson<MainSettings>();
                if (settings.Version == Consts.SettingsVersion)
                    return (settings, true);
            }
            catch (Exception ex)
            {
                Utils.Error(ex, "Error loading settings");
            }

            var backup = Consts.SettingsFile.AppendToName("-backup");
            await backup.DeleteAsync();
            
            await Consts.SettingsFile.CopyToAsync(backup);
            await Consts.SettingsFile.DeleteAsync();

            return default;
        }

        public static async ValueTask SaveSettings(MainSettings settings)
        {
            settings._saveSignal.OnNext(Unit.Default);

            // Might add this if people are putting save work on other threads or other
            // things that delay the operation.
            //settings._saveSignal.OnCompleted();
            //await settings._saveSignal;

            await settings.ToJsonAsync(Consts.SettingsFile);
        }
    }

    [JsonName("InstallerSettings")]
    public class InstallerSettings
    {
        public AbsolutePath LastInstalledListLocation { get; set; }
        public Dictionary<AbsolutePath, Mo2ModlistInstallationSettings> Mo2ModlistSettings { get; } = new Dictionary<AbsolutePath, Mo2ModlistInstallationSettings>();
    }

    [JsonName("Mo2ModListInstallerSettings")]
    public class Mo2ModlistInstallationSettings
    {
        public AbsolutePath InstallationLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        public bool AutomaticallyOverrideExistingInstall { get; set; }
    }

    [JsonName("CompilerSettings")]
    public class CompilerSettings
    {
        public ModManager LastCompiledModManager { get; set; }
        public AbsolutePath OutputLocation { get; set; }
        public MO2CompilationSettings MO2Compilation { get; } = new MO2CompilationSettings();
    }
    
    [JsonName("FiltersSettings")]
    [JsonObject(MemberSerialization.OptOut)]
    public class FiltersSettings : ViewModel
    {
        public bool ShowNSFW { get; set; }
        public bool OnlyInstalled { get; set; }
        public string Game { get; set; }
        public string Search { get; set; }
        private bool _isPersistent = true;
        public bool IsPersistent { get => _isPersistent; set => RaiseAndSetIfChanged(ref _isPersistent, value); }
        
        private bool _useCompression = false;
        public bool UseCompression { get => _useCompression; set => RaiseAndSetIfChanged(ref _useCompression, value); }
    }

    [JsonName("PerformanceSettings")]
    [JsonObject(MemberSerialization.OptOut)]
    public class PerformanceSettings : ViewModel
    {
        public PerformanceSettings()
        {
            _favorPerfOverRam = false;
            _diskThreads = Environment.ProcessorCount;
            _downloadThreads = Environment.ProcessorCount;
        }

        private int _downloadThreads;
        public int DownloadThreads { get => _downloadThreads; set => RaiseAndSetIfChanged(ref _downloadThreads, value); }
        
        private int _diskThreads;
        public int DiskThreads { get => _diskThreads; set => RaiseAndSetIfChanged(ref _diskThreads, value); }
        
        private bool _favorPerfOverRam;
        public bool FavorPerfOverRam { get => _favorPerfOverRam; set => RaiseAndSetIfChanged(ref _favorPerfOverRam, value); }


        public void SetProcessorSettings(ABatchProcessor processor)
        {
            processor.DownloadThreads = DownloadThreads;
            processor.DiskThreads = DiskThreads;
            processor.FavorPerfOverRam = FavorPerfOverRam;
        }
    }

    [JsonName("CompilationModlistSettings")]
    public class CompilationModlistSettings
    {
        public string ModListName { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
        public string Readme { get; set; }
        public bool IsNSFW { get; set; }
        public AbsolutePath SplashScreen { get; set; }
    }

    [JsonName("MO2CompilationSettings")]
    public class MO2CompilationSettings
    {
        public AbsolutePath DownloadLocation { get; set; }
        public AbsolutePath LastCompiledProfileLocation { get; set; }
        public Dictionary<AbsolutePath, CompilationModlistSettings> ModlistSettings { get; } = new Dictionary<AbsolutePath, CompilationModlistSettings>();
    }

}
