#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     What the login flow is allowed to write to the token store, and what it asks Nexus Mods for. The
///     store half used to write whatever came back from the token endpoint, and <c>AuthorizeToken</c>
///     returns null on any non-2xx - so a five-hundred from Nexus, or a proxy in the way, replaced the
///     stored login with an empty state. That was reachable before only by logging in while logged out,
///     where there was nothing to lose; it matters now because the Log in button no longer refuses while a
///     credential is stored, which is exactly what preflight's "your login has expired, log in again" row
///     asks the user to do.
///     <para>
///         The decision is a pure function so it can be checked here without a browser, an OAuth redirect or
///         a WPF <c>Application</c>.
///     </para>
/// </summary>
public class NexusOAuthLoginTests
{
    private const string StoredKey = "a-stored-api-key";

    [Fact]
    public void ARefusalWritesNothing()
    {
        Assert.Null(NexusOAuthLogin.StateToStore(Stored(), null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AReplyWithNothingToSendWritesNothing(string? accessToken)
    {
        var received = new JwtTokenReply {AccessToken = accessToken, RefreshToken = "a-refresh-token"};

        Assert.Null(NexusOAuthLogin.StateToStore(Stored(), received));
    }

    /// <summary>
    ///     The API key half of the login is not this exchange's business, and <c>NexusApi</c> falls back to
    ///     it when the OAuth half is unusable, so dropping it would log out a machine set up with one.
    /// </summary>
    [Fact]
    public void AGoodReplyKeepsTheStoredApiKey()
    {
        var state = NexusOAuthLogin.StateToStore(Stored(), Good());

        Assert.NotNull(state);
        Assert.Equal("a-new-access-token", state!.OAuth!.AccessToken);
        Assert.Equal(StoredKey, state.ApiKey);
    }

    [Fact]
    public void AGoodReplyWithNothingStoredIsStillALogin()
    {
        var state = NexusOAuthLogin.StateToStore(null, Good());

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
        var state = NexusOAuthLogin.StateToStore(null, Good());

        Assert.False(state!.OAuth!.IsExpired);
    }

    /// <summary>
    ///     The redirect the authorize request carries is the loopback one this app is actually listening on,
    ///     escaped as a query parameter. Nexus Mods sends the user back to whatever is in here, so a
    ///     redirect that does not match the open port is a login nothing receives.
    /// </summary>
    [Fact]
    public void TheAuthorizeUrlCarriesTheRedirectItWasGiven()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger());

        var query = QueryOf(NexusOAuthLogin.GenerateAuthorizeUrl(callback.RedirectUri, "a-challenge", "a-state"));

        Assert.Equal($"http://127.0.0.1:{callback.Port}/oauth/callback", query["redirect_uri"]);
        Assert.Equal(callback.RedirectUri, query["redirect_uri"]);
    }

    /// <summary>
    ///     PKCE, and the state that ties a redirect to the request that asked for it. Both are sent as this
    ///     app generated them; what checks them is the other end and <c>Login</c> respectively.
    /// </summary>
    [Fact]
    public void TheAuthorizeUrlAsksForACodeWithPkce()
    {
        var query = QueryOf(NexusOAuthLogin.GenerateAuthorizeUrl("http://127.0.0.1:1/oauth/callback",
            "a-challenge", "a-state"));

        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("a-challenge", query["code_challenge"]);
        Assert.Equal("a-state", query["state"]);
        Assert.Equal(NexusOAuthLogin.ClientId, query["client_id"]);
    }

    private static Dictionary<string, string> QueryOf(Uri uri)
    {
        var parsed = HttpUtility.ParseQueryString(uri.Query);
        return parsed.AllKeys.Where(k => k != null)
            .ToDictionary(k => k!, k => parsed[k] ?? string.Empty, StringComparer.Ordinal);
    }

    private static Microsoft.Extensions.Logging.ILogger NullLogger()
    {
        return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
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
