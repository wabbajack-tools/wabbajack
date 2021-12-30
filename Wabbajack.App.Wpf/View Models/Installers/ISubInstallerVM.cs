using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Installer;
using Wabbajack;
using Wabbajack.Interventions;

namespace Wabbajack
{
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
}
