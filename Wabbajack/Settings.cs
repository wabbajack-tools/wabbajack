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
            if (!Consts.SettingsFile.Exists)
            {
                settings = default;
                return false;
            }

            // Version check
            try
            {
                settings = Consts.SettingsFile.FromJson<MainSettings>();
                if (settings.Version == Consts.SettingsVersion)
                    return true;
            }
            catch (Exception ex)
            {
                Utils.Error(ex, "Error loading settings");
            }

            var backup = Consts.SettingsFile.AppendToName("-backup");
            backup.Delete();
            
            Consts.SettingsFile.CopyTo(backup);
            Consts.SettingsFile.Delete();

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

            Consts.SettingsFile.WriteAllText(JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
    }

    public class InstallerSettings
    {
        public AbsolutePath LastInstalledListLocation { get; set; }
        public Dictionary<AbsolutePath, Mo2ModlistInstallationSettings> Mo2ModlistSettings { get; } = new Dictionary<AbsolutePath, Mo2ModlistInstallationSettings>();
    }

    public class Mo2ModlistInstallationSettings
    {
        public AbsolutePath InstallationLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        public bool AutomaticallyOverrideExistingInstall { get; set; }
    }

    public class CompilerSettings
    {
        public ModManager LastCompiledModManager { get; set; }
        public AbsolutePath OutputLocation { get; set; }
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
        public string Readme { get; set; }
        public AbsolutePath SplashScreen { get; set; }
    }

    public class MO2CompilationSettings
    {
        public AbsolutePath DownloadLocation { get; set; }
        public AbsolutePath LastCompiledProfileLocation { get; set; }
        public Dictionary<AbsolutePath, CompilationModlistSettings> ModlistSettings { get; } = new Dictionary<AbsolutePath, CompilationModlistSettings>();
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
}
