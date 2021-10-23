using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader;

public static class ServiceExtensions
{
    public static IServiceCollection AddIPS4OAuth2Downloaders(this IServiceCollection services)
    {
        return services
            .AddAllSingleton<IDownloader, IDownloader<LoversLab>, LoversLabDownloader>()
            .AddAllSingleton<IDownloader, IDownloader<VectorPlexus>, VectorPlexusDownloader>();
    }
}