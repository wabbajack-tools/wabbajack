using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.Installer.Test;

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

        // Highest-priority downloaders that answer from a fake server, so the preflight download tests run
        // offline. Anything not registered with the server falls through to the real downloader.
        service.AddSingleton<FakeDownloadServer>();
        service.AddAllSingleton<IDownloader, IDownloader<Http>, FakeHttpDownloader>();
        service.AddAllSingleton<IDownloader, IDownloader<WabbajackCDN>, FakeCdnDownloader>();
        service.AddAllSingleton<IDownloader, IDownloader<Nexus>, FakeNexusDownloader>();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
}
