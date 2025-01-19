using CG.Web.MegaApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Bethesda;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.GoogleDrive;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.Downloaders.Manual;
using Wabbajack.Downloaders.MediaFire;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders
{
    public static class ServiceCollectionExtensions
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


        public static IServiceCollection AddGoogleDriveDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.GoogleDrive>,
                GoogleDriveDownloader>();
        }

        public static IServiceCollection AddBethesdaDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Bethesda>, IUrlDownloader, BethesdaDownloader>();
        }

        public static IServiceCollection AddGameFileDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<GameFileSource>,
                GameFileDownloader>();
        }

        public static IServiceCollection AddStandardGameLocator(this IServiceCollection services)
        {
            return services.AddAllSingleton<IGameLocator, GameLocator>();
        }

        public static IServiceCollection AddHttpDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Http>, HttpDownloader>();
        }

        public static IServiceCollection AddIPS4OAuth2Downloaders(this IServiceCollection services)
        {
            return services
                .AddAllSingleton<IDownloader, IDownloader<LoversLab>, LoversLabDownloader>()
                .AddAllSingleton<IDownloader, IDownloader<VectorPlexus>, VectorPlexusDownloader>();
        }

        public static IServiceCollection AddManualDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Manual>, ManualDownloader>();
        }

        public static IServiceCollection AddMediaFireDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.MediaFire>, MediaFireDownloader>();
        }

        public static IServiceCollection AddMegaDownloader(this IServiceCollection services)
        {
            return services
                .AddSingleton<MegaApiClient>()
                .AddAllSingleton<IDownloader, IDownloader<Mega>, MegaDownloader>();
        }

        public static IServiceCollection AddModDBDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.ModDB>, ModDBDownloader>();
        }

        public static IServiceCollection AddNexusDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<Nexus>, NexusDownloader>();
        }

        public static IServiceCollection AddWabbajackCDNDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<WabbajackCDN>, WabbajackCDNDownloader>();
        }
    }
}
