using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.MediaFire;

public static class ServiceExtensions
{
    public static IServiceCollection AddMediaFireDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.MediaFire>, MediaFireDownloader>();
    }
}