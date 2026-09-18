using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wabbajack.Downloaders.GameFile;

namespace Wabbajack.Networking.Bethesda.Steam;

public static class SteamAppTicketServiceExtensions
{
    /// <summary>
    ///     Registers the out-of-process ticket source, and with it the game file restorer that needs one.
    ///     Every registration is a TryAdd, so a host that wants its own timeouts or its own helper - a test,
    ///     or something that has a ticket already - registers those first and keeps them.
    ///     <para>
    ///         <see cref="CreationRestorer" /> is registered here rather than by
    ///         <see cref="ServiceExtensions.AddBethesdaCreations" /> because this is the call that decides
    ///         the question it turns on: whether this host is willing to talk to the local Steam client at
    ///         all. The restorer cannot work without a ticket source, so registering it anywhere else would
    ///         let a host end up with a restorer in the chain that throws the first time anything resolves
    ///         it. <see cref="ServiceExtensions.AddBethesdaCreations" /> is called from here for the same
    ///         reason, so the two halves cannot be half-wired; it is all TryAdds, so a host that calls it
    ///         itself - which is worth doing, because it says what is being added - loses nothing.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddSteamAppTicket(this IServiceCollection services)
    {
        services.AddBethesdaCreations();

        services.TryAddSingleton(new SteamAppTicketOptions());
        services.TryAddSingleton<ISteamAppTicketHelper, ProcessSteamAppTicketHelper>();
        services.TryAddSingleton<ISteamAppTicketSource, ChildProcessSteamAppTicketSource>();
        services.TryAddSingleton<ISteamClientPresence, SteamClientPresence>();

        services.TryAddSingleton<CreationCache>();
        services.AddGameFileRestorer<CreationRestorer>(GameFileRestorerOrder.Bethesda);
        return services;
    }
}
