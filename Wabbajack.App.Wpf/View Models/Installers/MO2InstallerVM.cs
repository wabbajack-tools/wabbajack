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
using Wabbajack.Installer;
using Wabbajack;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.Paths;
using Wabbajack.Util;

namespace Wabbajack
{
    public class MO2InstallerVM : ViewModel, ISubInstallerVM
    {
        public InstallerVM Parent { get; }
        
        [Reactive]
        public ErrorResponse CanInstall { get; set; }

        [Reactive]
        public IInstaller ActiveInstallation { get; private set; }


        [Reactive] public Mo2ModlistInstallationSettings CurrentSettings { get; set; }

        public FilePickerVM Location { get; }

        public FilePickerVM DownloadLocation { get; }

        public bool SupportsAfterInstallNavigation => true;

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
            Location.WhenAnyValue(t => t.TargetPath)
                .Subscribe(newPath =>
                {
                    if (newPath != default && DownloadLocation!.TargetPath == AbsolutePath.Empty)
                    {
                        DownloadLocation.TargetPath = newPath.Combine("downloads");
                    }
                }).DisposeWith(CompositeDisposable);
                
            DownloadLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select a location for MO2 downloads",
            };
            /* TODO
            DownloadLocation.AdditionalError = this.WhenAny(x => x.DownloadLocation.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));
            Location.AdditionalError = Observable.CombineLatest(
                    this.WhenAny(x => x.Location.TargetPath),
                    this.WhenAny(x => x.DownloadLocation.TargetPath),
                    resultSelector: (target, download) => (target, download))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(i => MO2Installer.CheckValidInstallPath(i.target, i.download, Parent.ModList?.SourceModList?.GameType.MetaData()))
                .ObserveOnGuiThread();

            _CanInstall = Observable.CombineLatest(
                    this.WhenAny(x => x.Location.ErrorState),
                    this.WhenAny(x => x.DownloadLocation.ErrorState),
                    installerVM.WhenAny(x => x.ModListLocation.ErrorState),
                    resultSelector: (loc, modlist, download) =>
                    {
                        return ErrorResponse.FirstFail(loc, modlist, download);
                    })
                .ToProperty(this, nameof(CanInstall));

            // Have Installation location updates modify the downloads location if empty or the same path
            this.WhenAny(x => x.Location.TargetPath)
                .Skip(1) // Don't do it initially
                .Subscribe(installPath =>
                {
                    if (DownloadLocation.TargetPath == default || DownloadLocation.TargetPath == installPath)
                    {
                        if (installPath.Exists) DownloadLocation.TargetPath = installPath.Combine("downloads");
                    }
                })
                .DisposeWith(CompositeDisposable);

            // Have Download location updates change if the same as the install path
            this.WhenAny(x => x.DownloadLocation.TargetPath)
                .Skip(1) // Don't do it initially
                .Subscribe(downloadPath =>
                {
                    if (downloadPath != default && downloadPath == Location.TargetPath)
                    {
                        DownloadLocation.TargetPath = Location.TargetPath.Combine("downloads");
                    }
                })
            .DisposeWith(CompositeDisposable);

            // Load settings
            _CurrentSettings = installerVM.WhenAny(x => x.ModListLocation.TargetPath)
                .Select(path => path == default ? null : installerVM.MWVM.Settings.Installer.Mo2ModlistSettings.TryCreate(path))
                .ToGuiProperty(this, nameof(CurrentSettings));
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
            this.WhenAny(x => x.ActiveInstallation)
                .Select(x => x?.LogMessages ?? Observable.Empty<IStatusMessage>())
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
                */
        }

        public void Unload()
        {
            SaveSettings(this.CurrentSettings);
        }

        private void SaveSettings(Mo2ModlistInstallationSettings settings)
        {
            //Parent.MWVM.Settings.Installer.LastInstalledListLocation = Parent.ModListLocation.TargetPath;
            if (settings == null) return;
            settings.InstallationLocation = Location.TargetPath;
            settings.DownloadLocation = DownloadLocation.TargetPath;
        }

        public void AfterInstallNavigation()
        {
            Process.Start("explorer.exe", Location.TargetPath.ToString());
        }

        public async Task<bool> Install()
        {
            /*
            using (var installer = new MO2Installer(
                archive: Parent.ModListLocation.TargetPath,
                modList: Parent.ModList.SourceModList,
                outputFolder: Location.TargetPath,
                downloadFolder: DownloadLocation.TargetPath,
                parameters: SystemParametersConstructor.Create()))
            {
                installer.Metadata = Parent.ModList.SourceModListMetadata;
                installer.UseCompression = Parent.MWVM.Settings.Filters.UseCompression;
                Parent.MWVM.Settings.Performance.SetProcessorSettings(installer);

                return await Task.Run(async () =>
                {
                    try
                    {
                        var workTask = installer.Begin();
                        ActiveInstallation = installer;
                        return await workTask;
                    }
                    finally
                    {
                        ActiveInstallation = null;
                    }
                });
            }
            */
            return true;
        }
        
        public IUserIntervention InterventionConverter(IUserIntervention intervention)
        {
            switch (intervention)
            {
                case ConfirmUpdateOfExistingInstall confirm:
                    return new ConfirmUpdateOfExistingInstallVM(this, confirm);
                default:
                    return intervention;
            }
        }
    }
}
