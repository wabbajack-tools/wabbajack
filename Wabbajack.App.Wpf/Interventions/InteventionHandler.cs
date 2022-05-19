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
        switch (intervention)
        {
            // Recast these or they won't be properly handled by the message bus
            case ManualDownload md:
                MessageBus.Current.SendMessage(md);
                break;
            case ManualBlobDownload bd:
                MessageBus.Current.SendMessage(bd);
                break;
            default:
                _logger.LogError("No handler for user intervention: {Type}", intervention);
                break;
        }
    }
}