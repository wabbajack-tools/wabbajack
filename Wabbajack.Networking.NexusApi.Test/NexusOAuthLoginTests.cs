#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Networking.NexusApi.OAuth;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     The Nexus Mods login as RFC 8252 asks for it: the browser is the user's own and the redirect comes
///     back over a loopback socket. Everything below drives that socket from this process rather than a
///     browser, and answers the token endpoint from a stub, so none of it needs the network or an account.
///     <para>
///         The one thing worth pinning hardest is that the redirect URI in the authorize request and the one
///         in the token exchange are the same string. Nexus Mods compares them and refuses the exchange if
///         they differ, and the failure - a token endpoint saying no, long after the user finished logging
///         in - looks nothing like its cause.
///     </para>
/// </summary>
public class NexusOAuthLoginTests
{
    private const string TokenReply =
        """{"access_token":"an-access-token","refresh_token":"a-refresh-token","expires_in":3600}""";

    [Fact]
    public async Task ALoginThatCompletesCarriesTheSameRedirectUriBothWays()
    {
        var endpoint = new StubTokenEndpoint();
        var login = Login(endpoint);
        var authorize = new List<Uri>();

        var result = await login.LogIn(Browser(authorize, q => $"code=a-code&state={q["state"]}"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Succeeded, result.Outcome);
        Assert.Equal("an-access-token", result.Token!.AccessToken);

        var sent = OAuthQuery.Parse(Assert.Single(authorize).Query);
        var exchanged = OAuthQuery.Parse(Assert.Single(endpoint.Bodies));

        Assert.Equal(sent["redirect_uri"], exchanged["redirect_uri"]);
        Assert.StartsWith("http://localhost:", sent["redirect_uri"]);
        Assert.EndsWith("/oauth/callback", sent["redirect_uri"]);
        Assert.Equal("a-code", exchanged["code"]);
    }

    /// <summary>
    ///     PKCE and the state are unchanged from the WebView2 version, which had them right; this is here so
    ///     that a later tidy-up of the authorize URL cannot quietly drop one.
    /// </summary>
    [Fact]
    public async Task TheAuthorizeRequestAsksForACodeWithAnS256Challenge()
    {
        var endpoint = new StubTokenEndpoint();
        var authorize = new List<Uri>();

        await Login(endpoint).LogIn(Browser(authorize, q => $"code=a-code&state={q["state"]}"),
            CancellationToken.None);

        var sent = OAuthQuery.Parse(Assert.Single(authorize).Query);
        Assert.Equal("code", sent["response_type"]);
        Assert.Equal("S256", sent["code_challenge_method"]);
        Assert.Equal("wabbajack", sent["client_id"]);
        Assert.NotEmpty(sent["code_challenge"]);
        Assert.NotEmpty(sent["state"]);

        // The verifier the exchange sends is what the challenge was made from, and neither is the other.
        var exchanged = OAuthQuery.Parse(Assert.Single(endpoint.Bodies));
        Assert.NotEqual(sent["code_challenge"], exchanged["code_verifier"]);
    }

    /// <summary>
    ///     A redirect carrying somebody else's state is not this login's, and nothing in it is read - least
    ///     of all its code, which is what an exchange would spend.
    /// </summary>
    [Fact]
    public async Task ARedirectWithTheWrongStateIsNotExchanged()
    {
        var endpoint = new StubTokenEndpoint();

        var result = await Login(endpoint).LogIn(Browser(new List<Uri>(), _ => "code=a-code&state=somebody-elses"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Failed, result.Outcome);
        Assert.Null(result.Token);
        Assert.Empty(endpoint.Bodies);
    }

    [Fact]
    public async Task ARedirectWithNoStateAtAllIsNotExchangedEither()
    {
        var endpoint = new StubTokenEndpoint();

        var result = await Login(endpoint).LogIn(Browser(new List<Uri>(), _ => "code=a-code"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Failed, result.Outcome);
        Assert.Empty(endpoint.Bodies);
    }

    /// <summary>
    ///     The user pressing Cancel on the consent screen. Nexus Mods redirects with an error rather than a
    ///     code, which is an ordinary ending rather than something to report as broken.
    /// </summary>
    [Fact]
    public async Task AnAccessDeniedRedirectEndsTheLoginWithoutAnExchange()
    {
        var endpoint = new StubTokenEndpoint();

        var result = await Login(endpoint).LogIn(
            Browser(new List<Uri>(),
                q => $"error=access_denied&error_description=The+user+said+no&state={q["state"]}"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Denied, result.Outcome);
        Assert.Equal("The user said no", result.Detail);
        Assert.Empty(endpoint.Bodies);
    }

    [Fact]
    public async Task ARedirectWithNoCodeInItIsNotALogin()
    {
        var endpoint = new StubTokenEndpoint();

        var result = await Login(endpoint).LogIn(Browser(new List<Uri>(), q => $"state={q["state"]}"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Failed, result.Outcome);
        Assert.Empty(endpoint.Bodies);
    }

    /// <summary>The user closed the browser and never came back.</summary>
    [Fact]
    public async Task ABrowserThatNeverComesBackTimesOut()
    {
        var login = Login(new StubTokenEndpoint(), TimeSpan.FromMilliseconds(250));

        var result = await login.LogIn(_ => { }, CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.TimedOut, result.Outcome);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task CancellingTheCallerEndsTheWait()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var result = await Login(new StubTokenEndpoint()).LogIn(_ => { }, cancel.Token);

        Assert.Equal(NexusOAuthOutcome.Cancelled, result.Outcome);
    }

    /// <summary>
    ///     Two logins at once would bind two ports and open two tabs, and the one the user finished would be
    ///     a coin toss. The second is refused rather than queued.
    /// </summary>
    [Fact]
    public async Task ASecondLoginWhileOneIsInFlightIsRefused()
    {
        var login = Login(new StubTokenEndpoint());
        var opened = new TaskCompletionSource();
        using var cancel = new CancellationTokenSource();

        var first = login.LogIn(_ => opened.TrySetResult(), cancel.Token);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var second = await login.LogIn(_ => Assert.Fail("A second browser was opened"), CancellationToken.None);
        Assert.Equal(NexusOAuthOutcome.AlreadyRunning, second.Outcome);

        await cancel.CancelAsync();
        Assert.Equal(NexusOAuthOutcome.Cancelled, (await first).Outcome);

        // And the one that was refused did not leave the guard held.
        var third = await login.LogIn(_ => { }, new CancellationToken(true));
        Assert.Equal(NexusOAuthOutcome.Cancelled, third.Outcome);
    }

    /// <summary>
    ///     A refusal from the token endpoint is not a login and is not an exception either - the callers of
    ///     this are a fire-and-forget task behind a settings button and a CLI verb.
    /// </summary>
    [Fact]
    public async Task ARefusalFromTheTokenEndpointIsAnOutcomeRatherThanAThrow()
    {
        var endpoint = new StubTokenEndpoint {Status = HttpStatusCode.InternalServerError};

        var result = await Login(endpoint).LogIn(Browser(new List<Uri>(), q => $"code=a-code&state={q["state"]}"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Failed, result.Outcome);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task ABodyThatIsNotATokenIsARefusalToo()
    {
        var endpoint = new StubTokenEndpoint {Body = "<html>we are having a bad day</html>"};

        var result = await Login(endpoint).LogIn(Browser(new List<Uri>(), q => $"code=a-code&state={q["state"]}"),
            CancellationToken.None);

        Assert.Equal(NexusOAuthOutcome.Failed, result.Outcome);
        Assert.Null(result.Token);
    }

    private static NexusOAuthLogin Login(StubTokenEndpoint endpoint, TimeSpan? timeout = null)
    {
        return new NexusOAuthLogin(NullLogger<NexusOAuthLogin>.Instance, new HttpClient(endpoint),
            timeout ?? TimeSpan.FromSeconds(30));
    }

    /// <summary>
    ///     Stands in for the system browser: records the authorize URL it was handed, then fetches the
    ///     redirect URI out of it with whatever <paramref name="reply" /> makes of the authorize parameters.
    ///     The fetch is started rather than awaited, because the flow only begins listening once this
    ///     returns.
    /// </summary>
    private static Action<Uri> Browser(List<Uri> authorize,
        Func<IReadOnlyDictionary<string, string>, string> reply)
    {
        return uri =>
        {
            authorize.Add(uri);
            var parameters = OAuthQuery.Parse(uri.Query);
            var redirect = $"{parameters["redirect_uri"]}?{reply(parameters)}";

            _ = Task.Run(async () =>
            {
                using var client = new HttpClient();
                try
                {
                    await client.GetStringAsync(redirect);
                }
                catch (HttpRequestException)
                {
                    // The flow closed the socket once it had what it came for. The browser tab losing its
                    // page is not this test's business.
                }
            });
        };
    }

    private sealed class StubTokenEndpoint : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string Body { get; init; } = TokenReply;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status) {Content = new StringContent(Body)};
        }
    }
}
