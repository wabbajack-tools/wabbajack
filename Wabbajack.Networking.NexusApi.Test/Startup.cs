using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.Networking.NexusApi.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddOSIntegrated();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}
