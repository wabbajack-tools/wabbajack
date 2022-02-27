using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Bethesda;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.Downloaders.Manual;
using Wabbajack.Downloaders.MediaFire;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.Downloaders;

public static class ServiceExtensions
{
    public static IServiceCollection AddDownloadDispatcher(this IServiceCollection services)
    {
        return services
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
            .AddManualDownloader()
            .AddSingleton<DownloadDispatcher>();
    }
}