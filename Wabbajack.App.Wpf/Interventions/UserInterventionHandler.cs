using System;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Interventions;

public class UserInterventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserInterventionHandler> _logger;

    public UserInterventionHandler(ILogger<UserInterventionHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
    }

    public void Raise(IUserIntervention intervention)
    {
        _logger.LogWarning("Unhandled user intervention: {Type}", intervention.GetType().Name);
    }
}
