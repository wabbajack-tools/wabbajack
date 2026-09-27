using System.Threading.Tasks;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Installer;

namespace Wabbajack.App.Avalonia.ViewModels.Installers;

public interface ISubInstallerVM
{
    InstallationVM Parent { get; }
    IInstaller ActiveInstallation { get; }
    void Unload();
    bool SupportsAfterInstallNavigation { get; }
    void AfterInstallNavigation();
    int ConfigVisualVerticalOffset { get; }
    ValidationResult CanInstall { get; }
    Task<bool> Install();
    IUserIntervention InterventionConverter(IUserIntervention intervention);
}
