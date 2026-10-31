using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test.Offline;

/// <summary>
///     How the Bethesda source wires itself into a host. <c>AddSteamAppTicket</c> is the call that says a
///     host will talk to the local Steam client, so it has to bring everything the Creation restorer needs
///     and leave alone whatever the host set up itself; a half-wired host would get a restorer that throws
///     the first time a user asks for a Creation.
/// </summary>
public class BethesdaServiceRegistrationTests
{
    private static int Count<T>(IServiceCollection services)
    {
        return services.Count(d => d.ServiceType == typeof(T));
    }

    private static GameFileRestorerRegistry Registry(IServiceCollection services)
    {
        return (GameFileRestorerRegistry) services
            .Single(d => d.ServiceType == typeof(GameFileRestorerRegistry)).ImplementationInstance!;
    }

    [Fact]
    public void TheTicketCallRegistersTheRestorerAndItsCache()
    {
        var services = new ServiceCollection();

        services.AddSteamAppTicket();

        Assert.Equal(1, Count<CreationRestorer>(services));
        Assert.Equal(1, Count<CreationCache>(services));
        Assert.Equal(1, Count<ISteamAppTicketSource>(services));
        Assert.Equal(1, Count<ISteamClientPresence>(services));
        Assert.Equal(1, Count<IGameFileRestorer>(services));
        Assert.Equal(new[] {typeof(CreationRestorer)}, Registry(services).Types);
    }

    /// <summary>The ticket call brings the table and the API client with it, so the two halves cannot be half-wired.</summary>
    [Fact]
    public void TheTicketCallImpliesTheCreationsTableAndClient()
    {
        var services = new ServiceCollection();

        services.AddSteamAppTicket();

        Assert.Equal(1, Count<CreationIndex>(services));
        Assert.Equal(1, Count<BethesdaApiClient>(services));
    }

    /// <summary>
    ///     Hosts call <c>AddBethesdaCreations</c> themselves and then the ticket call, and may call either
    ///     twice. Each service is still registered once, and the restorer is in the chain once.
    /// </summary>
    [Fact]
    public void CallingBothAndCallingTwiceRegistersEachOnce()
    {
        var services = new ServiceCollection();

        services.AddBethesdaCreations();
        services.AddSteamAppTicket();
        services.AddSteamAppTicket();

        Assert.Equal(1, Count<CreationIndex>(services));
        Assert.Equal(1, Count<BethesdaApiClient>(services));
        Assert.Equal(1, Count<SteamAppTicketOptions>(services));
        Assert.Equal(1, Count<CreationRestorer>(services));
        Assert.Equal(1, Count<CreationCache>(services));
        Assert.Equal(1, Count<IGameFileRestorer>(services));
        Assert.Equal(new[] {typeof(CreationRestorer)}, Registry(services).Types);
    }

    /// <summary>
    ///     A host's own timeouts are kept. The options exist so a test, or a host with a slow machine, does
    ///     not have to live with the defaults.
    /// </summary>
    [Fact]
    public void AHostsOwnTicketOptionsAreKept()
    {
        var mine = new SteamAppTicketOptions {TicketTimeout = TimeSpan.FromSeconds(3)};
        var services = new ServiceCollection();
        services.AddSingleton(mine);

        services.AddSteamAppTicket();

        var options = Assert.Single(services, d => d.ServiceType == typeof(SteamAppTicketOptions));
        Assert.Same(mine, options.ImplementationInstance);
    }

    /// <summary>The table alone is not a game file source: a host that asked only for it gets no restorer.</summary>
    [Fact]
    public void TheCreationsTableAloneRegistersNoRestorer()
    {
        var services = new ServiceCollection();

        services.AddBethesdaCreations();

        Assert.Equal(0, Count<CreationRestorer>(services));
        Assert.Equal(0, Count<IGameFileRestorer>(services));
        Assert.Equal(0, Count<ISteamAppTicketSource>(services));
    }
}
