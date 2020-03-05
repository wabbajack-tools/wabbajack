using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    [JsonObject(MemberSerialization.OptOut)]
    public class MainSettings
    {
        public byte Version { get; set; }

        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public InstallerSettings Installer { get; set; } = new InstallerSettings();
        public CompilerSettings Compiler { get; set; } = new CompilerSettings();
        public PerformanceSettings Performance { get; set; } = new PerformanceSettings();

        private Subject<Unit> _saveSignal = new Subject<Unit>();
        [JsonIgnore]
        public IObservable<Unit> SaveSignal => _saveSignal;

        public static bool TryLoadTypicalSettings(out MainSettings settings)
        {
            if (!File.Exists(Consts.SettingsFile))
            {
                settings = default;
                return false;
            }

            // Version check
            settings = JsonConvert.DeserializeObject<MainSettings>(File.ReadAllText(Consts.SettingsFile));
            if (settings.Version == Consts.SettingsVersion)
                return true;

            var backup = Consts.SettingsFile + "-backup.json";
            if(File.Exists(backup))
                File.Delete(backup);
            
            File.Copy(Consts.SettingsFile, backup);
            File.Delete(Consts.SettingsFile);

            settings = default;
            return false;
        }

        public static void SaveSettings(MainSettings settings)
        {
            settings._saveSignal.OnNext(Unit.Default);

            // Might add this if people are putting save work on other threads or other
            // things that delay the operation.
            //settings._saveSignal.OnCompleted();
            //await settings._saveSignal;

            File.WriteAllText(Consts.SettingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
    }

    public class InstallerSettings
    {
        public string LastInstalledListLocation { get; set; }
        public Dictionary<string, Mo2ModlistInstallationSettings> Mo2ModlistSettings { get; } = new Dictionary<string, Mo2ModlistInstallationSettings>();
        public SlideShowSettings SlideShowSettings { get; } = new SlideShowSettings
        {
            AllowNSFW = false,
            OnlyNSFW = false
        };
    }

    public class Mo2ModlistInstallationSettings
    {
        public string InstallationLocation { get; set; }
        public string DownloadLocation { get; set; }
        public bool AutomaticallyOverrideExistingInstall { get; set; }
    }

    public class CompilerSettings
    {
        public ModManager LastCompiledModManager { get; set; }
        public string OutputLocation { get; set; }
        public MO2CompilationSettings MO2Compilation { get; } = new MO2CompilationSettings();
        public VortexCompilationSettings VortexCompilation { get; } = new VortexCompilationSettings();
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class PerformanceSettings : ViewModel
    {
        private bool _manual;
        public bool Manual { get => _manual; set => RaiseAndSetIfChanged(ref _manual, value); }

        private byte _maxCores = byte.MaxValue;
        public byte MaxCores { get => _maxCores; set => RaiseAndSetIfChanged(ref _maxCores, value); }

        private Percent _targetUsage = Percent.One;
        public Percent TargetUsage { get => _targetUsage; set => RaiseAndSetIfChanged(ref _targetUsage, value); }

        public void AttachToBatchProcessor(ABatchProcessor processor)
        {
            processor.Add(
                this.WhenAny(x => x.Manual)
                    .Subscribe(processor.ManualCoreLimit));
            processor.Add(
                this.WhenAny(x => x.MaxCores)
                    .Subscribe(processor.MaxCores));
            processor.Add(
                this.WhenAny(x => x.TargetUsage)
                    .Subscribe(processor.TargetUsagePercent));
        }
    }

    public class CompilationModlistSettings
    {
        public string ModListName { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
        public bool ReadmeIsWebsite { get; set; }
        public string Readme { get; set; }
        public string SplashScreen { get; set; }
    }

    public class MO2CompilationSettings
    {
        public string DownloadLocation { get; set; }
        public string LastCompiledProfileLocation { get; set; }
        public Dictionary<string, CompilationModlistSettings> ModlistSettings { get; } = new Dictionary<string, CompilationModlistSettings>();
    }

    public class VortexCompilationSettings
    {
        public Game LastCompiledGame { get; set; }
        public Dictionary<Game, VortexGameSettings> ModlistSettings { get; } = new Dictionary<Game, VortexGameSettings>();
    }

    public class VortexGameSettings
    {
        public string GameLocation { get; set; }
        public string DownloadLocation { get; set; }
        public string StagingLocation { get; set; }
        public CompilationModlistSettings ModlistSettings { get; } = new CompilationModlistSettings();
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class SlideShowSettings : ViewModel
    {
        private bool _allowNSFW;
        public bool AllowNSFW { get => _allowNSFW; set => RaiseAndSetIfChanged(ref _allowNSFW, value); }

        private bool _onlyNSFW;
        public bool OnlyNSFW { get => _onlyNSFW; set => RaiseAndSetIfChanged(ref _onlyNSFW, value); }
    }
}
