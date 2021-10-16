using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddGameFileDownloader(this IServiceCollection services)
        {
            return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.GameFileSource>, 
                GameFileDownloader>();
        }

        public static IServiceCollection AddStandardGameLocator(this IServiceCollection services)
        {
            return services.AddAllSingleton<IGameLocator, GameLocator>();
        }
    }
}