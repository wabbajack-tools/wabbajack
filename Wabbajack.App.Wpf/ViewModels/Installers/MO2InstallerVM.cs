using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Installer;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Paths;
using System.Reactive.Linq;

namespace Wabbajack;

public class MO2InstallerVM : ViewModel, ISubInstallerVM
{
    public InstallationVM Parent { get; }

    [Reactive]
    public ValidationResult CanInstall { get; set; }

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

    public MO2InstallerVM(InstallationVM installerVM)
    {
        Parent = installerVM;

        Location = new FilePickerVM()
        {
            ExistCheckOption = FilePickerVM.CheckOptions.Off,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select a location to install Mod Organizer 2 to.",
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
            PromptTitle = "Select a location to store downloaded mod archives.",
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
