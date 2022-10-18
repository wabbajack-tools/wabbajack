using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Bethesda;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.Downloaders.Manual;
using Wabbajack.Downloaders.MediaFire;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public static class ServiceExtensions
{
    public static IServiceCollection AddDownloadDispatcher(this IServiceCollection services, bool useLoginDownloaders = true, bool useProxyCache = true)
    {
        if (useLoginDownloaders)
        {
            services
                .AddDTOConverters()
                .AddDTOSerializer()
                .AddGoogleDriveDownloader()
                .AddHttpDownloader()
                .AddMegaDownloader()
                .AddMediaFireDownloader()
                .AddModDBDownloader()
                .AddNexusDownloader()
                .AddIPS4OAuth2Downloaders()
                .AddWabbajackCDNDownloader()
                .AddGameFileDownloader()
                .AddBethesdaDownloader()
                .AddWabbajackClient()
                .AddManualDownloader();
        }
        else
        {
            services
                .AddDTOConverters()
                .AddDTOSerializer()
                .AddGoogleDriveDownloader()
                .AddHttpDownloader()
                .AddMegaDownloader()
                .AddMediaFireDownloader()
                .AddModDBDownloader()
                .AddWabbajackCDNDownloader()
                .AddWabbajackClient();
        }

        services.AddSingleton(s =>
            new DownloadDispatcher(s.GetRequiredService<ILogger<DownloadDispatcher>>(),
                s.GetRequiredService<IEnumerable<IDownloader>>(),
                s.GetRequiredService<IResource<DownloadDispatcher>>(),
                s.GetRequiredService<Client>(),
                s.GetRequiredService<IVerificationCache>(),
                useProxyCache));

        return services;
    }
}