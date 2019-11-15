using Newtonsoft.Json;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    [JsonObject(MemberSerialization.OptOut)]
    public class MainSettings
    {
        private static string Filename = "settings.json";

        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public InstallerSettings Installer { get; set; } = new InstallerSettings();
        public CompilerSettings Compiler { get; set; } = new CompilerSettings();

        [JsonIgnoreAttribute]
        private Subject<Unit> _saveSignal = new Subject<Unit>();
        public IObservable<Unit> SaveSignal => _saveSignal;

        public static MainSettings LoadSettings()
        {
            if (!File.Exists(Filename)) return new MainSettings();
            return JsonConvert.DeserializeObject<MainSettings>(File.ReadAllText(Filename));
        }

        public static void SaveSettings(MainSettings settings)
        {
            settings._saveSignal.OnNext(Unit.Default);

            // Might add this if people are putting save work on other threads or other
            // things that delay the operation.
            //settings._saveSignal.OnCompleted();
            //await settings._saveSignal;

            File.WriteAllText(Filename, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
    }

    public class InstallerSettings
    {
        public string LastInstalledListLocation { get; set; }
        public Dictionary<string, ModlistInstallationSettings> ModlistSettings { get; } = new Dictionary<string, ModlistInstallationSettings>();
    }

    public class ModlistInstallationSettings
    {
        public string InstallationLocation { get; set; }
        public string StagingLocation { get; set; }
        public string DownloadLocation { get; set; }
    }

    public class CompilerSettings
    {
        public ModManager LastCompiledModManager { get; set; }
        public MO2CompilationSettings MO2Compilation { get; } = new MO2CompilationSettings();
        public VortexCompilationSettings VortexCompilation { get; } = new VortexCompilationSettings();
    }

    public class CompilationModlistSettings
    {
        public string ModListName { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
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
        public string StagingLocation { get; set; }
        public Game LastCompiledGame { get; set; }
        public Dictionary<Game, CompilationModlistSettings> ModlistSettings { get; } = new Dictionary<Game, CompilationModlistSettings>();
    }
}
