using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddIPS4OAuth2Downloaders(this IServiceCollection services)
        {
            
            return services
                .AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.LoversLab>, LoversLabDownloader>()
                .AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.VectorPlexus>, VectorPlexusDownloader>();
        }
    }
}