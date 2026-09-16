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

        // One of possibly several game file sources rather than the only one. Registered this way, a host
        // that adds nothing else resolves this restorer itself and behaves exactly as it always has, while
        // a host that also registers the Bethesda source gets both in order behind one IGameFileRestorer.
        services.AddGameFileRestorer<SteamGameFileRestorer>(GameFileRestorerOrder.Steam);
        return services;
    }
}
