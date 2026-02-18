using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.App.Avalonia.Interventions;

/// <summary>
/// Stub intervention handler — logs unhandled interventions until the full
/// browser/login UI is wired up in a later milestone.
/// </summary>
public class UserInterventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserInterventionHandler> _logger;

    public UserInterventionHandler(ILogger<UserInterventionHandler> logger)
    {
        _logger = logger;
    }

    public void Raise(IUserIntervention intervention)
    {
        _logger.LogWarning("Unhandled user intervention: {Type}", intervention.GetType().Name);
    }
}
