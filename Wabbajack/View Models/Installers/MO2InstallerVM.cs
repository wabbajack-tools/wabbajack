using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.UI;
using Wabbajack.Util;

namespace Wabbajack
{
    public class MO2InstallerVM : ViewModel, ISubInstallerVM
    {
        public InstallerVM Parent { get; }

        public IObservable<bool> CanInstall { get; }

        [Reactive]
        public AInstaller ActiveInstallation { get; private set; }

        private readonly ObservableAsPropertyHelper<Mo2ModlistInstallationSettings> _CurrentSettings;
        public Mo2ModlistInstallationSettings CurrentSettings => _CurrentSettings.Value;

        public FilePickerVM Location { get; }

        public FilePickerVM DownloadLocation { get; }

        public bool SupportsAfterInstallNavigation => true;

        [Reactive]
        public bool AutomaticallyOverwrite { get; set; }

        public int ConfigVisualVerticalOffset => 25;

        public MO2InstallerVM(InstallerVM installerVM)
        {
            Parent = installerVM;

            Location = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Installation Directory",
            };
            DownloadLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select a location for MO2 downloads",
            };
            DownloadLocation.AdditionalError = this.WhenAny(x => x.DownloadLocation.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));
            Location.AdditionalError = Observable.CombineLatest(
                    this.WhenAny(x => x.Location.TargetPath),
                    this.WhenAny(x => x.DownloadLocation.TargetPath),
                    resultSelector: (target, download) => (target, download))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(i => MO2Installer.CheckValidInstallPath(i.target, i.download))
                .ObserveOnGuiThread();

            CanInstall = Observable.CombineLatest(
                this.WhenAny(x => x.Location.InError),
                this.WhenAny(x => x.DownloadLocation.InError),
                installerVM.WhenAny(x => x.ModListLocation.InError),
                resultSelector: (loc, modlist, download) =>
                {
                    return !loc && !download && !modlist;
                });

            // Have Installation location updates modify the downloads location if empty
            this.WhenAny(x => x.Location.TargetPath)
                .Skip(1) // Don't do it initially
                .Subscribe(installPath =>
                {
                    if (string.IsNullOrWhiteSpace(DownloadLocation.TargetPath))
                    {
                        DownloadLocation.TargetPath = Path.Combine(installPath, "downloads");
                    }
                })
                .DisposeWith(CompositeDisposable);

            // Load settings
            _CurrentSettings = installerVM.WhenAny(x => x.ModListLocation.TargetPath)
                .Select(path => path == null ? null : installerVM.MWVM.Settings.Installer.Mo2ModlistSettings.TryCreate(path))
                .ToProperty(this, nameof(CurrentSettings));
            this.WhenAny(x => x.CurrentSettings)
                .Pairwise()
                .Subscribe(settingsPair =>
                {
                    SaveSettings(settingsPair.Previous);
                    if (settingsPair.Current == null) return;
                    Location.TargetPath = settingsPair.Current.InstallationLocation;
                    DownloadLocation.TargetPath = settingsPair.Current.DownloadLocation;
                    AutomaticallyOverwrite = settingsPair.Current.AutomaticallyOverrideExistingInstall;
                })
                .DisposeWith(CompositeDisposable);
            installerVM.MWVM.Settings.SaveSignal
                .Subscribe(_ => SaveSettings(CurrentSettings))
                .DisposeWith(CompositeDisposable);

            // Hook onto user interventions, and intercept MO2 specific ones for customization
            this.WhenAny(x => x.ActiveInstallation.LogMessages)
                .Switch()
                .Subscribe(x =>
                {
                    switch (x)
                    {
                        case ConfirmUpdateOfExistingInstall c:
                            if (AutomaticallyOverwrite)
                            {
                                c.Confirm();
                            }
                            break;
                        default:
                            break;
                    }
                })
                .DisposeWith(CompositeDisposable);
        }

        public void Unload()
        {
            SaveSettings(this.CurrentSettings);
        }

        private void SaveSettings(Mo2ModlistInstallationSettings settings)
        {
            Parent.MWVM.Settings.Installer.LastInstalledListLocation = Parent.ModListLocation.TargetPath;
            if (settings == null) return;
            settings.InstallationLocation = Location.TargetPath;
            settings.DownloadLocation = DownloadLocation.TargetPath;
            settings.AutomaticallyOverrideExistingInstall = AutomaticallyOverwrite;
        }

        public void AfterInstallNavigation()
        {
            Process.Start("explorer.exe", Location.TargetPath);
        }

        public async Task Install()
        {
            var installer = new MO2Installer(
                archive: Parent.ModListLocation.TargetPath,
                modList: Parent.ModList.SourceModList,
                outputFolder: Location.TargetPath,
                downloadFolder: DownloadLocation.TargetPath,
                parameters: SystemParametersConstructor.Create())
            {
                ManualCoreLimit = Parent.MWVM.Settings.Performance.Manual,
                MaxCores = Parent.MWVM.Settings.Performance.MaxCores,
                TargetUsagePercent = Parent.MWVM.Settings.Performance.TargetUsage,
            };

            await Task.Run(async () =>
            {
                try
                {
                    var workTask = installer.Begin();
                    ActiveInstallation = installer;
                    await workTask;
                    return ErrorResponse.Success;
                }
                finally
                {
                    ActiveInstallation = null;
                }
            });
        }
    }
}
