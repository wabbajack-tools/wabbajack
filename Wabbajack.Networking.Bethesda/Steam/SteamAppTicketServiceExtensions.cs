using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wabbajack.Networking.Bethesda.Steam;

public static class SteamAppTicketServiceExtensions
{
    /// <summary>
    ///     Registers the out-of-process ticket source. Every registration is a TryAdd, so a host that wants
    ///     its own timeouts or its own helper - a test, or something that has a ticket already - registers
    ///     those first and keeps them.
    /// </summary>
    public static IServiceCollection AddSteamAppTicket(this IServiceCollection services)
    {
        services.TryAddSingleton(new SteamAppTicketOptions());
        services.TryAddSingleton<ISteamAppTicketHelper, ProcessSteamAppTicketHelper>();
        services.TryAddSingleton<ISteamAppTicketSource, ChildProcessSteamAppTicketSource>();
        return services;
    }
}
