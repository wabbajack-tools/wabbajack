using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders;

public static class ServiceExtensions
{
    public static IServiceCollection AddNexusDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<Nexus>, NexusDownloader>();
    }
}