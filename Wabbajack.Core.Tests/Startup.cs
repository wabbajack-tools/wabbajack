using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.Core.Tests;

/// <summary>
/// Default startup for all tests in this assembly that don't have a namespace-specific Startup.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
        services.AddSingleton<IUserInterventionHandler, CancellingInterventionHandler>();
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
