using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Installer;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Paths;

namespace Wabbajack
{
    public class MO2InstallerVM : ViewModel, ISubInstallerVM
    {
        public InstallerVM Parent { get; }

        [Reactive]
        public ErrorResponse CanInstall { get; set; }

        [Reactive]
        public IInstaller ActiveInstallation { get; private set; }

        [Reactive]
        public Mo2ModlistInstallationSettings CurrentSettings { get; set; }

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
            settings.AutomaticallyOverrideExistingInstall = AutomaticallyOverwrite;
        }

        public void AfterInstallNavigation()
        {
            UIUtils.OpenFolder(Location.TargetPath);
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
