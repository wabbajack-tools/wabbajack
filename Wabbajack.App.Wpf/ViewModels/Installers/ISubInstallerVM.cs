using System.Threading.Tasks;
using Wabbajack.Installer;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack;

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
