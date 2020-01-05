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
    public class MainSettings : ViewModel
    {
        private static string _filename = "settings.json";

        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public InstallerSettings Installer { get; set; } = new InstallerSettings();
        public CompilerSettings Compiler { get; set; } = new CompilerSettings();

        private Subject<Unit> _saveSignal = new Subject<Unit>();
        [JsonIgnore]
        public IObservable<Unit> SaveSignal => _saveSignal;

        public static bool TryLoadTypicalSettings(out MainSettings settings)
        {
            if (!File.Exists(_filename))
            {
                settings = default;
                return false;
            }
            settings = JsonConvert.DeserializeObject<MainSettings>(File.ReadAllText(_filename));
            return true;
        }

        public static void SaveSettings(MainSettings settings)
        {
            settings._saveSignal.OnNext(Unit.Default);

            // Might add this if people are putting save work on other threads or other
            // things that delay the operation.
            //settings._saveSignal.OnCompleted();
            //await settings._saveSignal;

            File.WriteAllText(_filename, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
    }

    public class InstallerSettings
    {
        public string LastInstalledListLocation { get; set; }
        public Dictionary<string, Mo2ModlistInstallationSettings> Mo2ModlistSettings { get; } = new Dictionary<string, Mo2ModlistInstallationSettings>();
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
}
