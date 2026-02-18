using ReactiveUI.Fody.Helpers;

namespace Wabbajack.App.Avalonia.ViewModels.Installer;

public class MO2InstallerVM : ViewModelBase
{
    [Reactive] public string InstallPath { get; set; } = "";
    [Reactive] public string DownloadPath { get; set; } = "";
}
