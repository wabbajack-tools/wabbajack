using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wabbajack.Downloaders.GameFile;

namespace Wabbajack.Networking.Steam;

public static class ServiceExtensions
{
    /// <summary>
    ///     Registers the Steam session, the content client and the game file restorer that sits on top of
    ///     them. Every registration is a TryAdd, so a host that wants its own prompt - or that calls this
    ///     twice because something it depends on already did - gets one of each rather than a surprise.
    /// </summary>
    public static IServiceCollection AddSteam(this IServiceCollection services)
    {
        services.TryAddSingleton<ISteamGuardPrompt, InterventionSteamGuardPrompt>();

        services.TryAddSingleton<SteamSession>();
        services.TryAddSingleton<ISteamSession>(s => s.GetRequiredService<SteamSession>());
        services.TryAddSingleton<SteamContentClient>();
        services.TryAddSingleton<ISteamContentClient>(s => s.GetRequiredService<SteamContentClient>());
        services.TryAddSingleton<ISteamManifestIndex, ClientSteamManifestIndex>();
        services.TryAddSingleton<IGameFileRestorer, SteamGameFileRestorer>();
        return services;
    }
}
