using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ConfirmUpdateOfExistingInstallVM : ViewModel, IUserIntervention
    {
        public ConfirmUpdateOfExistingInstall Source { get; }

        public MO2InstallerVM Installer { get; }

        public bool Handled => ((IUserIntervention)Source).Handled;

        public int CpuID => ((IUserIntervention)Source).CpuID;

        public DateTime Timestamp => ((IUserIntervention)Source).Timestamp;

        public string ShortDescription => ((IUserIntervention)Source).ShortDescription;

        public string ExtendedDescription => ((IUserIntervention)Source).ExtendedDescription;

        public ConfirmUpdateOfExistingInstallVM(MO2InstallerVM installer, ConfirmUpdateOfExistingInstall confirm)
        {
            Source = confirm;
            Installer = installer;
        }

        public void Cancel()
        {
            ((IUserIntervention)Source).Cancel();
        }
    }
}
