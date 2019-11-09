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
        public string LastInstalledListLocation { get; set; }
        public Dictionary<string, InstallationSettings> InstallationSettings { get; } = new Dictionary<string, InstallationSettings>();
        public string LastCompiledProfileLocation { get; set; }
        public Dictionary<string, CompilationSettings> CompilationSettings { get; } = new Dictionary<string, CompilationSettings>();

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

    public class InstallationSettings
    {
        public string InstallationLocation { get; set; }
        public string DownloadLocation { get; set; }
    }

    public class CompilationSettings
    {
        public string ModListName { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
        public string Readme { get; set; }
        public string SplashScreen { get; set; }
        public string DownloadLocation { get; set; }
    }
}
