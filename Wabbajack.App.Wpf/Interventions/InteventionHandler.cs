using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Interventions;

public class InteventionHandler : IUserInterventionHandler
{
    private readonly ILogger<InteventionHandler> _logger;

    public InteventionHandler(ILogger<InteventionHandler> logger)
    {
        _logger = logger;
    }
    public void Raise(IUserIntervention intervention)
    {
        // Recast these or they won't be properly handled by the message bus
        if (intervention is ManualDownload md)
            MessageBus.Current.SendMessage(md);
    }
}