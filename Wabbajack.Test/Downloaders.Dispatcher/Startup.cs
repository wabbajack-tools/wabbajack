using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.Downloaders.Dispatcher.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddOSIntegrated();
        service.AddSingleton<IUserInterventionHandler, CancellingInterventionHandler>();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }

    private class CancellingInterventionHandler : IUserInterventionHandler
    {
        public void Raise(IUserIntervention intervention)
        {
            intervention.Cancel();
        }
    }
}