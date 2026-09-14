using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wabbajack.Networking.Steam;

public static class ServiceExtensions
{
    public static IServiceCollection AddSteam(this IServiceCollection services)
    {
        // TryAdd, so a host that wants its own prompt can register one before calling this.
        services.TryAddSingleton<ISteamGuardPrompt, InterventionSteamGuardPrompt>();

        services.AddSingleton<SteamSession>();
        services.AddSingleton<ISteamSession>(s => s.GetRequiredService<SteamSession>());
        services.AddSingleton<SteamContentClient>();
        return services;
    }
}
