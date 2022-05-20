using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;
using Wabbajack.UserIntervention;

namespace Wabbajack.Interventions;

public class UserInteventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserInteventionHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UserInteventionHandler(ILogger<UserInteventionHandler> logger, IServiceProvider serviceProvider)
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
                var view = new BrowserWindow();
                var provider = _serviceProvider.GetRequiredService<ManualDownloadHandler>();
                view.DataContext = provider;
                provider.Intervention = md;
                view.Show();
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