using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.ManualSources;

public static class ServiceExtensions
{
    /// <summary>
    ///     Registers the metadata-only downloaders for every source that needs a browser. They sit at the
    ///     lowest priority, so any full downloader registered for the same state is picked ahead of them.
    /// </summary>
    public static IServiceCollection AddManualSourceDownloaders(this IServiceCollection services)
    {
        return services
            .AddAllSingleton<IDownloader, IDownloader<Manual>, ManualSourceDownloader>()
            .AddAllSingleton<IDownloader, IDownloader<MediaFire>, MediaFireSource>()
            .AddAllSingleton<IDownloader, IDownloader<Mega>, MegaSource>()
            .AddAllSingleton<IDownloader, IDownloader<GoogleDrive>, GoogleDriveSource>()
            .AddAllSingleton<IDownloader, IDownloader<ModDB>, ModDBSource>()
            .AddAllSingleton<IDownloader, IDownloader<LoversLab>, LoversLabSource>()
            .AddAllSingleton<IDownloader, IDownloader<VectorPlexus>, VectorPlexusSource>()
            .AddAllSingleton<IDownloader, IDownloader<Bethesda>, BethesdaSource>();
    }
}
