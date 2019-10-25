using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerVM : ViewModel, IDataErrorInfo
    {
        public MainWindowVM MWVM { get; }

        public RunMode Mode => RunMode.Compile;

        private string _Mo2Folder;
        public string Mo2Folder { get => _Mo2Folder; set => this.RaiseAndSetIfChanged(ref _Mo2Folder, value); }

        private string _ModListName;
        public string ModListName { get => _ModListName; set => this.RaiseAndSetIfChanged(ref _ModListName, value); }

        private string _Location;
        public string Location { get => _Location; set => this.RaiseAndSetIfChanged(ref _Location, value); }

        private bool _UIReady = true;
        public bool UIReady { get => _UIReady; set => this.RaiseAndSetIfChanged(ref _UIReady, value); }

        private string _ModName;
        public string ModName { get => _ModName; set => this.RaiseAndSetIfChanged(ref _ModName, value); }

        private string _AuthorName;
        public string AuthorName { get => _AuthorName; set => this.RaiseAndSetIfChanged(ref _AuthorName, value); }

        private string _Summary;
        public string Summary { get => _Summary; set => this.RaiseAndSetIfChanged(ref _Summary, value); }

        private string _ImagePath;
        public string ImagePath { get => _ImagePath; set => this.RaiseAndSetIfChanged(ref _ImagePath, value); }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        private string _NexusSiteURL;
        public string NexusSiteURL { get => _NexusSiteURL; set => this.RaiseAndSetIfChanged(ref _NexusSiteURL, value); }

        private string _ReadMeText;
        public string ReadMeText { get => _ReadMeText; set => this.RaiseAndSetIfChanged(ref _ReadMeText, value); }

        private string _HTMLReport;
        public string HTMLReport { get => _HTMLReport; set => this.RaiseAndSetIfChanged(ref _HTMLReport, value); }

        private string _DownloadLocation;
        public string DownloadLocation { get => _DownloadLocation; set => this.RaiseAndSetIfChanged(ref _DownloadLocation, value); }

        public IReactiveCommand BeginCommand { get; }
        public IReactiveCommand ChangePathCommand { get; }
        public IReactiveCommand ChangeDownloadPathCommand { get; }
        public IReactiveCommand ChangeSplashScreenCommand { get; }

        public CompilerVM(MainWindowVM mainWindowVM, string source)
        {
            this.MWVM = mainWindowVM;
            this.Location = Path.GetDirectoryName(source);

            this.BeginCommand = ReactiveCommand.Create(
                execute: this.ExecuteBegin,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());
            this.ChangePathCommand = ReactiveCommand.Create(
                ExecuteChangePath,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());
            this.ChangeDownloadPathCommand = ReactiveCommand.Create(
                ExecuteChangeDownloadPath,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());
            this.ChangeSplashScreenCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread(),
                execute: () =>
                {
                    this.ImagePath = UIUtils.OpenFileDialog("Banner image|*.png");
                });

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
        }

        private void ExecuteChangePath()
        {
            Location = UIUtils.ShowFolderSelectionDialog("Select Your MO2 profile directory");
        }

        private void ExecuteChangeDownloadPath()
        {
            var folder = UIUtils.ShowFolderSelectionDialog("Select a location for MO2 downloads");
            if (folder != null)
            {
                DownloadLocation = folder;
            }
        }

        private void ConfigureForBuild()
        {
            var profile_folder = Path.GetDirectoryName(Location);
            this.Mo2Folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(this.Mo2Folder, "ModOrganizer.exe")))
                this.Log().Error($"Error! No ModOrganizer2.exe found in {this.Mo2Folder}");

            var profile_name = Path.GetFileName(profile_folder);
            this.ModListName = profile_name;

            var tmp_compiler = new Compiler(this.Mo2Folder);
            DownloadLocation = tmp_compiler.MO2DownloadsFolder;
        }

        private void ExecuteBegin()
        {
            if (this.Mo2Folder != null)
            {
                var compiler = new Compiler(this.Mo2Folder)
                {
                    MO2Profile = this.ModListName,
                    ModListName = this.ModName,
                    ModListAuthor = this.AuthorName,
                    ModListDescription = this.Summary,
                    ModListImage = this.ImagePath,
                    ModListWebsite = this.NexusSiteURL,
                    ModListReadme = this.ReadMeText,
                };
                var th = new Thread(() =>
                {
                    UIReady = false;
                    try
                    {
                        compiler.Compile();
                        if (compiler.ModList != null && compiler.ModList.ReportHTML != null)
                        {
                            this.HTMLReport = compiler.ModList.ReportHTML;
                        }
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        this.Log().Warn(ex, "Can't continue");
                    }
                    finally
                    {
                        UIReady = true;
                    }
                })
                {
                    Priority = ThreadPriority.BelowNormal
                };
                th.Start();
            }
            else
            {
                this.Log().Warn("Cannot compile modlist: no valid Mod Organizer profile directory selected.");
                UIReady = true;
            }
        }

        public string Error => "Error";

        public string this[string columnName] => Validate(columnName);

        private string Validate(string columnName)
        {
            string validationMessage = null;
            switch (columnName)
            {
                case "Location":
                    if (Location == null)
                    {
                        validationMessage = null;
                    }
                    else switch (Mode)
                        {
                            case RunMode.Compile when Location != null && Directory.Exists(Location) && File.Exists(Path.Combine(Location, "modlist.txt")):
                                Location = Path.Combine(Location, "modlist.txt");
                                validationMessage = null;
                                ConfigureForBuild();
                                break;
                            case RunMode.Install when Location != null && Directory.Exists(Location) && !Directory.EnumerateFileSystemEntries(Location).Any():
                                validationMessage = null;
                                break;
                            case RunMode.Install when Location != null && Directory.Exists(Location) && Directory.EnumerateFileSystemEntries(Location).Any():
                                validationMessage = "You have selected a non-empty directory. Installing the modlist here might result in a broken install!";
                                break;
                            default:
                                validationMessage = "Invalid Mod Organizer profile directory";
                                break;
                        }
                    break;
            }
            return validationMessage;
        }
    }
}
