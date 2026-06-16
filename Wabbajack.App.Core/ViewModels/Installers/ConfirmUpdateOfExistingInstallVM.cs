using System;
using System.Threading;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack;

public class ConfirmUpdateOfExistingInstallVM : ViewModel, IUserIntervention
{
    public ConfirmUpdateOfExistingInstall Source { get; }

    public MO2InstallerVM Installer { get; }

    public bool Handled => ((IUserIntervention)Source).Handled;
    public CancellationToken Token { get; }
    public void SetException(Exception exception)
    {
        throw new NotImplementedException();
    }

    public int CpuID => 0;

    public DateTime Timestamp => DateTime.Now;

    public string ShortDescription => "Short Desc";

    public string ExtendedDescription => "Extended Desc";

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
