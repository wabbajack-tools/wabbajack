using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Http;

public static class ServiceExtensions
{
    public static IServiceCollection AddHttpDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Http>, HttpDownloader>();
    }
}