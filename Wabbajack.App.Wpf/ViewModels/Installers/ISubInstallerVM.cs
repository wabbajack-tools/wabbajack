using System.Threading.Tasks;
using Wabbajack.Installer;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack;

public interface ISubInstallerVM
{
    InstallerVM Parent { get; }
    IInstaller ActiveInstallation { get; }
    void Unload();
    bool SupportsAfterInstallNavigation { get; }
    void AfterInstallNavigation();
    int ConfigVisualVerticalOffset { get; }
    ErrorResponse CanInstall { get; }
    Task<bool> Install();
    IUserIntervention InterventionConverter(IUserIntervention intervention);
}
