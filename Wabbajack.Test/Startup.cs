using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.Test;

// Default Xunit.DependencyInjection Startup for the consolidated test assembly.
// Tests in nested namespaces (e.g. Wabbajack.Compiler.Test) use their own Startup
// via Xunit.DependencyInjection's per-namespace discovery; this is the fallback
// for tests whose namespace has no specific Startup.
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}
