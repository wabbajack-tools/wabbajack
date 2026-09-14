using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Services.OSIntegrated;
using Xunit;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

// NexusCredentialTests moves this process' own NEXUS_API_KEY, which every other test here authenticates
// with. Sharing a collection with NexusApiTests only serialises it against the classes that opt in, and a
// class added later would not: nothing in xunit makes a test class join a collection it does not name. One
// assembly-wide switch is the guard that cannot be forgotten. The suite is IO-bound and small, so the
// parallelism is worth little here anyway.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

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
