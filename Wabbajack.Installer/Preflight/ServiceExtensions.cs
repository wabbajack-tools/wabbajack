using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight;

public static class ServiceExtensions
{
    /// <summary>
    ///     Registers the preflight checks and the Downloads-folder acquirer. The host must also register an
    ///     <see cref="INexusLoginProbe" /> and an <see cref="IDownloadPolicySource" />; the production ones
    ///     live in Wabbajack.Services.OSIntegrated. A host can register its own
    ///     <see cref="ManualDownloadAcquirerOptions" /> to tune the acquirer; otherwise the defaults apply.
    /// </summary>
    public static IServiceCollection AddPreflight(this IServiceCollection services)
    {
        services.AddSingleton<IPreflightCheck, NexusLoginCheck>();
        services.AddSingleton<IPreflightCheck, GameInstalledCheck>();
        services.AddSingleton<IPreflightCheck, GameFilesCheck>();
        services.AddSingleton<IPreflightCheck, ArchiveInventoryCheck>();
        services.AddSingleton<IPreflightCheck, UnsupportedArchivesCheck>();
        services.AddSingleton<IPreflightCheck, AutomatedDownloadsCheck>();
        services.AddSingleton<IPreflightCheck, ManualDownloadsCheck>();
        services.AddSingleton<IPreflightCheck, DiskSpaceCheck>();

        // Transient: an acquirer belongs to one preflight run, and the runner disposes it with the run.
        services.AddTransient<IManualDownloadAcquirer>(s => new ManualDownloadAcquirer(
            s.GetRequiredService<ILogger<ManualDownloadAcquirer>>(),
            s.GetRequiredService<FileHashCache>(),
            s.GetRequiredService<IResource<FileHashCache>>(),
            s.GetRequiredService<DownloadDispatcher>(),
            s.GetService<ManualDownloadAcquirerOptions>() ?? new ManualDownloadAcquirerOptions()));
        return services;
    }
}
