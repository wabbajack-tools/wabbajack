using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GoogleDrive;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders;

public static class GoogleDriveServiceExtensions
{
    public static IServiceCollection AddGoogleDriveDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<DTOs.DownloadStates.GoogleDrive>,
            GoogleDriveDownloader>();
    }
}