#nullable enable
using System;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.UserIntervention;
using Xunit;

namespace Wabbajack.App.Wpf.Test;

/// <summary>
///     What the login window is allowed to write to the token store. It used to write whatever came back
///     from the token endpoint, and <c>AuthorizeToken</c> returns null on any non-2xx - so a five-hundred
///     from Nexus, or a proxy in the way, replaced the stored login with an empty state. That was reachable
///     before only by logging in while logged out, where there was nothing to lose; it matters now because
///     the Log in button no longer refuses while a credential is stored, which is exactly what preflight's
///     "your login has expired, log in again" row asks the user to do.
///     <para>
///         The decision is a pure function so it can be checked here without a WebView2, an OAuth redirect
///         or a WPF <c>Application</c>.
///     </para>
/// </summary>
public class NexusLoginHandlerTests
{
    private const string StoredKey = "a-stored-api-key";

    [Fact]
    public void ARefusalWritesNothing()
    {
        Assert.Null(NexusLoginHandler.StateToStore(Stored(), null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AReplyWithNothingToSendWritesNothing(string? accessToken)
    {
        var received = new JwtTokenReply {AccessToken = accessToken, RefreshToken = "a-refresh-token"};

        Assert.Null(NexusLoginHandler.StateToStore(Stored(), received));
    }

    /// <summary>
    ///     The API key half of the login is not this exchange's business, and <c>NexusApi</c> falls back to
    ///     it when the OAuth half is unusable, so dropping it would log out a machine set up with one.
    /// </summary>
    [Fact]
    public void AGoodReplyKeepsTheStoredApiKey()
    {
        var state = NexusLoginHandler.StateToStore(Stored(), Good());

        Assert.NotNull(state);
        Assert.Equal("a-new-access-token", state!.OAuth!.AccessToken);
        Assert.Equal(StoredKey, state.ApiKey);
    }

    [Fact]
    public void AGoodReplyWithNothingStoredIsStillALogin()
    {
        var state = NexusLoginHandler.StateToStore(null, Good());

        Assert.NotNull(state);
        Assert.Equal("a-new-access-token", state!.OAuth!.AccessToken);
        Assert.Equal(string.Empty, state.ApiKey);
    }

    /// <summary>
    ///     Stamped on the way in, because expiry is measured from it: an unstamped reply reads as received
    ///     at the zero of the file-time epoch, which is to say expired the moment it is written.
    /// </summary>
    [Fact]
    public void AGoodReplyIsStampedWithTheTimeItArrived()
    {
        var state = NexusLoginHandler.StateToStore(null, Good());

        Assert.False(state!.OAuth!.IsExpired);
    }

    private static NexusOAuthState Stored()
    {
        return new NexusOAuthState
        {
            ApiKey = StoredKey,
            OAuth = new JwtTokenReply
            {
                AccessToken = "the-login-they-already-had",
                RefreshToken = "the-refresh-token-they-already-had",
                ReceivedAt = DateTime.UtcNow.ToFileTimeUtc(),
                ExpiresIn = 3600
            }
        };
    }

    private static JwtTokenReply Good()
    {
        return new JwtTokenReply
        {
            AccessToken = "a-new-access-token", RefreshToken = "a-new-refresh-token", ExpiresIn = 3600
        };
    }
}
