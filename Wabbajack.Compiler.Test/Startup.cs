using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
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

        // Every request a compile makes goes through this one client; answered locally, the tests need no network.
        service.AddSingleton(s => new HttpClient(
            new Offline.OfflineServers(s.GetRequiredService<Wabbajack.Networking.WabbajackClientApi.Configuration>())));
        service.AddSingleton<ITokenProvider<WabbajackApiState>, Offline.FixedApiToken>();

        service.AddScoped<ModListHarness>();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}