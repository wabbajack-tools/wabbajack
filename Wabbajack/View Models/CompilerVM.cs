using Microsoft.WindowsAPICodePack.Dialogs;
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

        public FilePickerVM Location { get; }

        [Reactive]
        public bool UIReady { get; set; } = true;

        [Reactive]
        public string AuthorName { get; set; }

        [Reactive]
        public string Summary { get; set; } = "Description (700 characters max)";

        public FilePickerVM ImagePath { get; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        [Reactive]
        public string Website { get; set; }

        public FilePickerVM ReadMeText { get; }

        [Reactive]
        public string HTMLReport { get; set; }

        public FilePickerVM DownloadLocation { get; }

        public IReactiveCommand BeginCommand { get; }

        public CompilerVM(MainWindowVM mainWindowVM, string source)
        {
            this.MWVM = mainWindowVM;
            this.Location = new FilePickerVM()
            {
                TargetPath = source,
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.File,
            };
            this.DownloadLocation = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.Folder,
            };
            this.ImagePath = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.File,
                Filters =
                {
                    new CommonFileDialogFilter("Banner image", "*.png")
                }
            };
            this.ReadMeText = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                DoExistsCheck = true,
            };

            this.BeginCommand = ReactiveCommand.CreateFromTask(
                execute: this.ExecuteBegin,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());

            this._Image = this.WhenAny(x => x.ImagePath.TargetPath)
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
            this.ReadMeText.TargetPath = settings.Readme;
            this.ImagePath.TargetPath = settings.SplashScreen;
            this.Website = settings.Website;
            if (!string.IsNullOrWhiteSpace(settings.DownloadLocation))
            {
                this.DownloadLocation.TargetPath = settings.DownloadLocation;
            }
            if (!string.IsNullOrWhiteSpace(settings.Location))
            {
                this.Location.TargetPath = settings.Location;
            }
            this.MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.Author = this.AuthorName;
                    settings.ModListName = this.ModListName;
                    settings.Description = this.Summary;
                    settings.Readme = this.ReadMeText.TargetPath;
                    settings.SplashScreen = this.ImagePath.TargetPath;
                    settings.Website = this.Website;
                    settings.Location = this.Location.TargetPath;
                    settings.DownloadLocation = this.DownloadLocation.TargetPath;
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
            this.DownloadLocation.TargetPath = tmp_compiler.MO2DownloadsFolder;
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
                    ModListImage = this.ImagePath.TargetPath,
                    ModListWebsite = this.Website,
                    ModListReadme = this.ReadMeText.TargetPath,
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
