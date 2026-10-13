using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.ViewModels.Installers;

/// <summary>The install and downloads folder pickers the installer's configuration page shows.</summary>
public partial class MO2InstallerVM : ViewModel, ISubInstallerVM
{
    public InstallationVM Parent { get; }

    [Reactive] public partial ValidationResult CanInstall { get; set; }

    [Reactive] public partial IInstaller ActiveInstallation { get; private set; }

    [Reactive] public partial Mo2ModlistInstallationSettings CurrentSettings { get; set; }

    public FilePickerVM Location { get; }

    public FilePickerVM DownloadLocation { get; }

    public bool SupportsAfterInstallNavigation => true;

    [Reactive] public partial bool AutomaticallyOverwrite { get; set; }

    public int ConfigVisualVerticalOffset => 25;

    public MO2InstallerVM(InstallationVM installerVM)
    {
        Parent = installerVM;

        Location = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.Off,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select a location to install Mod Organizer 2 to."
        };
        Location.WhenAnyValue(t => t.TargetPath)
            .Subscribe(newPath =>
            {
                if (newPath != default && DownloadLocation!.TargetPath == AbsolutePath.Empty)
                {
                    DownloadLocation.TargetPath = newPath.Combine("downloads");
                }
            }).DisposeWith(CompositeDisposable);

        DownloadLocation = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.Off,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select a location to store downloaded mod archives."
        };
    }

    public void Unload()
    {
        SaveSettings(CurrentSettings);
    }

    private void SaveSettings(Mo2ModlistInstallationSettings settings)
    {
        if (settings == null) return;
        settings.InstallationLocation = Location.TargetPath;
        settings.DownloadLocation = DownloadLocation.TargetPath;
        settings.AutomaticallyOverrideExistingInstall = AutomaticallyOverwrite;
    }

    public void AfterInstallNavigation()
    {
        Process.Start("explorer.exe", Location.TargetPath.ToString());
    }

    /// <summary>The install runs through <see cref="InstallationVM" />; this was already a stub in WPF.</summary>
    public async Task<bool> Install()
    {
        return true;
    }

    /// <summary>
    ///     WPF wrapped a ConfirmUpdateOfExistingInstall intervention in a view model here. Nothing in either app
    ///     raises that intervention, so it and its view model were not carried over, and every intervention
    ///     passes through unchanged.
    /// </summary>
    public IUserIntervention InterventionConverter(IUserIntervention intervention)
    {
        return intervention;
    }
}

[JsonName("Mo2ModListInstallerSettings")]
public class Mo2ModlistInstallationSettings
{
    public AbsolutePath InstallationLocation { get; set; }
    public AbsolutePath DownloadLocation { get; set; }
    public bool AutomaticallyOverrideExistingInstall { get; set; }
}
