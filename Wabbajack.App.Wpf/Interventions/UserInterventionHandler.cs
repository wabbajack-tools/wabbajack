using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Messages;
using Wabbajack.UserIntervention;

namespace Wabbajack.Interventions;

public class UserIntreventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserIntreventionHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UserIntreventionHandler(ILogger<UserIntreventionHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    public void Raise(IUserIntervention intervention)
    {
        switch (intervention)
        {
            // Recast these or they won't be properly handled by the message bus
            case ManualDownload md:
            {
                var provider = _serviceProvider.GetRequiredService<ManualDownloadHandler>();
                provider.Intervention = md;
                MessageBus.Current.SendMessage(new SpawnBrowserWindow(provider));
                break;
            }
            case ManualBlobDownload bd:
            {
                var provider = _serviceProvider.GetRequiredService<ManualBlobDownloadHandler>();
                provider.Intervention = bd;
                MessageBus.Current.SendMessage(new SpawnBrowserWindow(provider));
                break;
            }
            default:
                _logger.LogError("No handler for user intervention: {Type}", intervention);
                break;

        }

    }
}