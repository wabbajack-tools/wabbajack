using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam.UserInterventions;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Everything about the session that can be reached without talking to Steam: what it does with the
///     stored token before it ever opens a socket, and what logging out reports.
/// </summary>
public class SteamSessionTests
{
    private static SteamSession MakeSession(FakeTokenProvider tokens)
    {
        return new SteamSession(NullLogger<SteamSession>.Instance, tokens, new SilentPrompt());
    }

    [Fact]
    public async Task LoggingInWithNoStoredTokenAsksForAFreshLogin()
    {
        using var session = MakeSession(new FakeTokenProvider());

        var ex = await Assert.ThrowsAsync<SteamLoginRequiredException>(() =>
            session.LoginWithStoredTokenAsync(CancellationToken.None));

        Assert.Contains("no saved Steam login", ex.Message);
    }

    [Fact]
    public async Task LoggingInWithAnEmptyStoredTokenAsksForAFreshLogin()
    {
        var tokens = new FakeTokenProvider
        {
            Stored = new SteamLoginState {AccountName = "someaccount", RefreshToken = "   "}
        };
        using var session = MakeSession(tokens);

        await Assert.ThrowsAsync<SteamLoginRequiredException>(() =>
            session.LoginWithStoredTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnExpiredStoredTokenIsDiscardedBeforeAnythingIsDialled()
    {
        var tokens = new FakeTokenProvider
        {
            Stored = new SteamLoginState
            {
                AccountName = "someaccount",
                RefreshToken = "header.payload.signature",
                RefreshTokenExpiresAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(1)
            }
        };
        using var session = MakeSession(tokens);

        var ex = await Assert.ThrowsAsync<SteamLoginRequiredException>(() =>
            session.LoginWithStoredTokenAsync(CancellationToken.None));

        Assert.Contains("expired", ex.Message);
        Assert.True(tokens.DeleteCalled);
        Assert.Null(tokens.Stored);
    }

    [Fact]
    public async Task LoggingOutWithNothingStoredSaysSo()
    {
        var tokens = new FakeTokenProvider();
        using var session = MakeSession(tokens);

        Assert.Equal(SteamLogoutResult.NothingStored, await session.LogoutAsync());
    }

    [Fact]
    public async Task LoggingOutDeletesAStoredToken()
    {
        var tokens = new FakeTokenProvider
        {
            Stored = new SteamLoginState {AccountName = "someaccount", RefreshToken = "a.b.c"}
        };
        using var session = MakeSession(tokens);

        Assert.Equal(SteamLogoutResult.Deleted, await session.LogoutAsync());
        Assert.Null(tokens.Stored);
    }

    [Fact]
    public async Task LoggingOutAdmitsWhenTheCredentialComesFromTheEnvironment()
    {
        // EncryptedJsonTokenProvider answers HaveToken() for an environment variable that Delete() has no
        // way to remove. Reporting a clean logout there would leave a working credential behind.
        var tokens = new FakeTokenProvider {EnvironmentToken = true};
        using var session = MakeSession(tokens);

        Assert.Equal(SteamLogoutResult.HeldInEnvironment, await session.LogoutAsync());
    }

    [Fact]
    public async Task LoggingOutAdmitsTheEnvironmentEvenWhenAFileWasAlsoDeleted()
    {
        var tokens = new FakeTokenProvider
        {
            Stored = new SteamLoginState {AccountName = "someaccount", RefreshToken = "a.b.c"},
            EnvironmentToken = true
        };
        using var session = MakeSession(tokens);

        Assert.Equal(SteamLogoutResult.HeldInEnvironment, await session.LogoutAsync());
        Assert.Null(tokens.Stored);
    }

    [Fact]
    public void HaveStoredTokenFollowsTheProvider()
    {
        var tokens = new FakeTokenProvider();
        using var session = MakeSession(tokens);

        Assert.False(session.HaveStoredToken);

        tokens.Stored = new SteamLoginState {AccountName = "someaccount", RefreshToken = "a.b.c"};
        Assert.True(session.HaveStoredToken);
    }

    [Fact]
    public void CredentialsNeverPrintTheirPassword()
    {
        // A positional record would generate a ToString that prints every property, so one structured log
        // line would put the password in the log file.
        var credentials = new SteamCredentials("someaccount", "hunter2");

        Assert.DoesNotContain("hunter2", credentials.ToString());
        Assert.Contains("someaccount", credentials.ToString());
        Assert.DoesNotContain("hunter2", string.Format("{0}", credentials));
    }

    private class FakeTokenProvider : ITokenProvider<SteamLoginState>
    {
        public SteamLoginState? Stored { get; set; }

        /// <summary>
        ///     Stands in for the environment variable fallback: visible to HaveToken, untouchable by Delete.
        /// </summary>
        public bool EnvironmentToken { get; init; }

        public bool DeleteCalled { get; private set; }

        public ValueTask<SteamLoginState?> Get()
        {
            if (Stored == null && !EnvironmentToken)
                throw new InvalidOperationException("No login data");
            return ValueTask.FromResult(Stored);
        }

        public ValueTask SetToken(SteamLoginState val)
        {
            Stored = val;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> Delete()
        {
            DeleteCalled = true;
            if (Stored == null) return ValueTask.FromResult(false);
            Stored = null;
            return ValueTask.FromResult(true);
        }

        public bool HaveToken()
        {
            return Stored != null || EnvironmentToken;
        }
    }

    private class SilentPrompt : ISteamGuardPrompt
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
            return Task.FromResult(true);
        }
    }
}
