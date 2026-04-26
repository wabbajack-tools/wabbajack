using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;
using Configuration = Wabbajack.Services.OSIntegrated.Configuration;

namespace Wabbajack.Compiler.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        service.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });

        service.AddScoped<ModListHarness>();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}