using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Xunit;

namespace Wabbajack.Networking.Steam.Test.Offline;

/// <summary>
///     How <see cref="ServiceExtensions.AddSteam" /> wires itself into a host. The app registers its own
///     Steam Guard prompt before calling it, and more than one thing may call it; getting either wrong means
///     a login that throws instead of asking for a code, or two Steam connections fighting over one account.
/// </summary>
public class SteamServiceRegistrationTests
{
    private static int Count<T>(IServiceCollection services)
    {
        return services.Count(d => d.ServiceType == typeof(T));
    }

    /// <summary>Calling it twice - a host and something it depends on - registers each service once.</summary>
    [Fact]
    public void CallingItTwiceRegistersEachServiceOnce()
    {
        var services = new ServiceCollection();

        services.AddSteam();
        services.AddSteam();

        Assert.Equal(1, Count<ISteamGuardPrompt>(services));
        Assert.Equal(1, Count<SteamSession>(services));
        Assert.Equal(1, Count<ISteamSession>(services));
        Assert.Equal(1, Count<SteamContentClient>(services));
        Assert.Equal(1, Count<ISteamContentClient>(services));
        Assert.Equal(1, Count<ISteamManifestIndex>(services));
        Assert.Equal(1, Count<SteamGameFileRestorer>(services));
        Assert.Equal(1, Count<IGameFileRestorer>(services));
        Assert.Equal(1, Count<GameFileRestorerRegistry>(services));

        var registry = (GameFileRestorerRegistry) services
            .Single(d => d.ServiceType == typeof(GameFileRestorerRegistry)).ImplementationInstance!;
        Assert.Equal(new[] {typeof(SteamGameFileRestorer)}, registry.Types);
    }

    /// <summary>
    ///     The app registers its own prompt first because the default one raises an intervention the app
    ///     answers by throwing. Replacing it would make every Steam Guard login fail.
    /// </summary>
    [Fact]
    public void AHostsOwnSteamGuardPromptIsKept()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISteamGuardPrompt, HostPrompt>();

        services.AddSteam();

        var prompt = Assert.Single(services, d => d.ServiceType == typeof(ISteamGuardPrompt));
        Assert.Equal(typeof(HostPrompt), prompt.ImplementationType);
    }

    [Fact]
    public void WithNoHostPromptTheInterventionPromptIsTheDefault()
    {
        var services = new ServiceCollection();

        services.AddSteam();

        var prompt = Assert.Single(services, d => d.ServiceType == typeof(ISteamGuardPrompt));
        Assert.Equal(typeof(InterventionSteamGuardPrompt), prompt.ImplementationType);
    }

    /// <summary>
    ///     Everything that asks for the session gets the same one. Two would mean two connections and two
    ///     login states, so a user who logged in through one would look logged out to the other.
    /// </summary>
    [Fact]
    public void TheSessionAndItsInterfaceAreTheSameSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ITokenProvider<SteamLoginState>, NoStoredLogin>();
        services.AddSingleton<ISteamGuardPrompt, HostPrompt>();
        services.AddSteam();

        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<SteamSession>();
        var asInterface = provider.GetRequiredService<ISteamSession>();

        Assert.Same(concrete, asInterface);
        Assert.Same(asInterface, provider.GetRequiredService<ISteamSession>());
    }

    /// <summary>
    ///     A host with only Steam gets the Steam restorer itself rather than a wrapper, so every message
    ///     still names Steam as its source.
    /// </summary>
    [Fact]
    public void WithOnlySteamTheRestorerIsTheSteamOneItself()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ISteamSession, FakeSession>();
        services.AddSingleton<ISteamContentClient, FakeContentClient>();
        services.AddSingleton<ISteamManifestIndex, FakeIndex>();
        services.AddSteam();

        using var provider = services.BuildServiceProvider();

        var restorer = provider.GetRequiredService<IGameFileRestorer>();
        Assert.IsType<SteamGameFileRestorer>(restorer);
        Assert.Equal("Steam", restorer.SourceName);
    }

    private sealed class HostPrompt : ISteamGuardPrompt
    {
        public Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class NoStoredLogin : ITokenProvider<SteamLoginState>
    {
        public ValueTask<SteamLoginState?> Get()
        {
            return ValueTask.FromResult<SteamLoginState?>(null);
        }

        public ValueTask SetToken(SteamLoginState val)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> Delete()
        {
            return ValueTask.FromResult(false);
        }

        public bool HaveToken()
        {
            return false;
        }
    }
}
