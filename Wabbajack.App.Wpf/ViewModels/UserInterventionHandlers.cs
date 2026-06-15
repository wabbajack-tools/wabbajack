using System;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IDialogService _dialogService;

    public UserInterventionHandlers(ILogger<UserInterventionHandlers> logger, MainWindowVM mvm, IDialogService dialogService)
    {
        _logger = logger;
        MainWindow = mvm;
        _dialogService = dialogService;
    }
    public async Task Handle(IStatusMessage msg)
    {
        switch (msg)
        {
            case CriticalFailureIntervention c:
                _dialogService.ShowError(c.ExtendedDescription, c.ShortDescription);
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
