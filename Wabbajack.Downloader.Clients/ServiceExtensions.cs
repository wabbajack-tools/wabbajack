using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Downloader.Services;

public static class ServiceExtensions
{
    public static void AddDownloaderService(this IServiceCollection services)
    {
        services.AddHttpClient("SmallFilesClient").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
        services.AddSingleton<IDownloadClientFactory, DownloadClientFactory>();
        services.AddSingleton<IHttpDownloader, DownloaderService>();
    }
}