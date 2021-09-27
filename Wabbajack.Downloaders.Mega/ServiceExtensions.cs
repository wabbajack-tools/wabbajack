using CG.Web.MegaApiClient;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.ModDB
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddMegaDownloader(this IServiceCollection services)
        {
            
            return services
                .AddSingleton<MegaApiClient>()
                .AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.Mega>, MegaDownloader>();
        }
    }
}