using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Installer.Preflight.Checks;

namespace Wabbajack.Installer.Preflight;

public static class ServiceExtensions
{
    /// <summary>
    ///     Registers the preflight checks. The host must also register an <see cref="INexusLoginProbe" /> and
    ///     an <see cref="IDownloadPolicySource" />; the production ones live in Wabbajack.Services.OSIntegrated.
    /// </summary>
    public static IServiceCollection AddPreflight(this IServiceCollection services)
    {
        services.AddSingleton<IPreflightCheck, NexusLoginCheck>();
        services.AddSingleton<IPreflightCheck, GameInstalledCheck>();
        services.AddSingleton<IPreflightCheck, GameFilesCheck>();
        services.AddSingleton<IPreflightCheck, ArchiveInventoryCheck>();
        services.AddSingleton<IPreflightCheck, UnsupportedArchivesCheck>();
        services.AddSingleton<IPreflightCheck, DiskSpaceCheck>();
        return services;
    }
}
