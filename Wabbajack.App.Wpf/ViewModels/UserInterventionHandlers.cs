using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.Messages;

namespace Wabbajack;

public class UserInterventionHandlers
{
    public MainWindowVM MainWindow { get; }
    private readonly ILogger<UserInterventionHandlers> _logger;

    public UserInterventionHandlers(ILogger<UserInterventionHandlers> logger, MainWindowVM mvm)
    {
        _logger = logger;
        MainWindow = mvm;
    }
    public async Task Handle(IStatusMessage msg)
    {
        switch (msg)
        {
            case CriticalFailureIntervention c:
                MessageBox.Show(c.ExtendedDescription, c.ShortDescription, MessageBoxButton.OK,
                    MessageBoxImage.Error);
                c.Cancel();
                if (c.ExitApplication) await MainWindow.ShutdownApplication();
                break;
            case ConfirmationIntervention c:
                break;
            default:
                throw new NotImplementedException($"No handler for {msg}");
        }
    }
    
}
