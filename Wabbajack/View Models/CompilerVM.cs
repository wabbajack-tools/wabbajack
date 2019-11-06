using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerVM : ViewModel
    {
        public MainWindowVM MWVM { get; }

        [Reactive]
        public string Mo2Folder { get; set; }

        [Reactive]
        public string MOProfile { get; set; }

        [Reactive]
        public string ModListName { get; set; }

        [Reactive]
        public string Location { get; set; }

        [Reactive]
        public bool UIReady { get; set; } = true;

        [Reactive]
        public string AuthorName { get; set; }

        [Reactive]
        public string Summary { get; set; } = "Description (700 characters max)";

        [Reactive]
        public string ImagePath { get; set; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        [Reactive]
        public string Website { get; set; }

        [Reactive]
        public string ReadMeText { get; set; }

        [Reactive]
        public string HTMLReport { get; set; }

        [Reactive]
        public string DownloadLocation { get; set; }

        public IReactiveCommand BeginCommand { get; }

        public CompilerVM(MainWindowVM mainWindowVM, string source)
        {
            this.MWVM = mainWindowVM;
            this.Location = source;

            this.BeginCommand = ReactiveCommand.CreateFromTask(
                execute: this.ExecuteBegin,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());

            this._Image = this.WhenAny(x => x.ImagePath)
                .Select(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return UIUtils.BitmapImageFromResource("Wabbajack.Resources.Banner_Dark.png");
                    if (UIUtils.TryGetBitmapImageFromFile(path, out var image))
                    {
                        return image;
                    }
                    return UIUtils.BitmapImageFromResource("Wabbajack.Resources.none.png");
                })
                .ToProperty(this, nameof(this.Image));

            ConfigureForBuild(source);

            // Load settings
            CompilationSettings settings = this.MWVM.Settings.CompilationSettings.TryCreate(source);
            this.AuthorName = settings.Author;
            this.ModListName = settings.ModListName;
            this.Summary = settings.Description;
            this.ReadMeText = settings.Readme;
            this.ImagePath = settings.SplashScreen;
            this.Website = settings.Website;
            if (!string.IsNullOrWhiteSpace(settings.DownloadLocation))
            {
                this.DownloadLocation = settings.DownloadLocation;
            }
            if (!string.IsNullOrWhiteSpace(settings.Location))
            {
                this.Location = settings.Location;
            }
            this.MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.Author = this.AuthorName;
                    settings.ModListName = this.ModListName;
                    settings.Description = this.Summary;
                    settings.Readme = this.ReadMeText;
                    settings.SplashScreen = this.ImagePath;
                    settings.Website = this.Website;
                    settings.Location = this.Location;
                    settings.DownloadLocation = this.DownloadLocation;
                })
                .DisposeWith(this.CompositeDisposable);
        }

        private void ConfigureForBuild(string location)
        {
            var profile_folder = Path.GetDirectoryName(location);
            this.Mo2Folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(this.Mo2Folder, "ModOrganizer.exe")))
            {
                Utils.Log($"Error! No ModOrganizer2.exe found in {this.Mo2Folder}");
            }

            this.MOProfile = Path.GetFileName(profile_folder);
            this.ModListName = this.MOProfile;

            var tmp_compiler = new Compiler(this.Mo2Folder);
            this.DownloadLocation = tmp_compiler.MO2DownloadsFolder;
        }

        private async Task ExecuteBegin()
        {
            if (this.Mo2Folder != null)
            {
                var compiler = new Compiler(this.Mo2Folder)
                {
                    MO2Profile = this.MOProfile,
                    ModListName = this.ModListName,
                    ModListAuthor = this.AuthorName,
                    ModListDescription = this.Summary,
                    ModListImage = this.ImagePath,
                    ModListWebsite = this.Website,
                    ModListReadme = this.ReadMeText,
                };
                await Task.Run(() =>
                {
                    UIReady = false;
                    try
                    {
                        compiler.Compile();
                        if (compiler.ModList?.ReportHTML != null)
                        {
                            this.HTMLReport = compiler.ModList.ReportHTML;
                        }
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log($"Can't continue: {ex.ExceptionToString()}");
                    }
                    finally
                    {
                        UIReady = true;
                    }
                });
            }
            else
            {
                Utils.Log("Cannot compile modlist: no valid Mod Organizer profile directory selected.");
                UIReady = true;
            }
        }
    }
}
