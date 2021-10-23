using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.ModDB;

public static class ServiceExtensions
{
    public static IServiceCollection AddModDBDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.ModDB>, ModDBDownloader>();
    }
}