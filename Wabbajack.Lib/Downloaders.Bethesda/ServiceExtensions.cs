using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Bethesda;

public static class ServiceExtensions
{
    public static IServiceCollection AddBethesdaDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Bethesda>, IUrlDownloader, BethesdaDownloader>();
    }
}