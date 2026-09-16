using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wabbajack.Networking.Bethesda;

public static class ServiceExtensions
{
    /// <summary>
    ///     Registers the committed Creations table and the client that walks Bethesda's chain with it.
    ///     Every registration is a TryAdd, so a host that wants its own of any of them keeps it.
    ///     <para>
    ///         <see cref="ISteamAppTicketSource" /> is deliberately not registered here. Minting a ticket
    ///         means loading the game's own native Steam library, which is a decision a host makes rather
    ///         than something a client library should make for it, and leaving it out means this half of
    ///         the project stays resolvable and testable without Steam anywhere in reach.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddBethesdaCreations(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => CreationIndex.Default);
        services.TryAddSingleton<BethesdaApiClient>();
        return services;
    }
}
