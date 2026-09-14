#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     What a refresh is allowed to do to the login on disk. An expired access token is refreshed on the way
///     to every authenticated call, and the WPF login tile now asks for the credential as it is constructed,
///     so this runs when the app starts rather than only when something is downloaded. Storing whatever came
///     back therefore turned any bad minute - Nexus down, a laptop opened before its network is up - into a
///     login the user has to make again: the error body deserializes into a reply whose access token is null,
///     and that went over the top of a refresh token that would have worked on the next attempt.
///     <para>
///         No test here leaves the machine: the refresh endpoint is answered by a handler in the file.
///     </para>
/// </summary>
public class NexusRefreshTokenTests
{
    private const string RefreshToken = "the-refresh-token";

    [Fact]
    public async Task ARefusedRefreshLeavesTheStoredLoginExactlyAsItWas()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, _) = Api(store, HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");

        var source = await api.CredentialSource();

        // Nothing to send, so nothing is claimed...
        Assert.Equal(NexusCredentialSource.None, source);
        // ...and nothing is written: the refresh token is still there for the next attempt.
        Assert.Equal(0, store.Writes);
        Assert.Equal(RefreshToken, store.Stored.OAuth!.RefreshToken);
    }

    /// <summary>
    ///     The shape that made this so easy to miss: Nexus answers 200 with a body that is not a token. It
    ///     deserializes happily into a reply with a null access token, which is what used to be stored.
    /// </summary>
    [Fact]
    public async Task ARefreshThatReturnsNoAccessTokenStoresNothing()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, _) = Api(store, HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}");

        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(0, store.Writes);
        Assert.Equal(RefreshToken, store.Stored.OAuth!.RefreshToken);
    }

    /// <summary>
    ///     A body that is not JSON at all - a proxy's error page, a captive portal - is a refusal like any
    ///     other rather than an exception out of whatever asked for the credential.
    /// </summary>
    [Fact]
    public async Task ARefreshThatReturnsSomethingElseEntirelyStoresNothing()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, _) = Api(store, HttpStatusCode.OK, "<html>who knows</html>");

        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(0, store.Writes);
    }

    /// <summary>The API key half of the same login is untouched by any of this, and is still a login.</summary>
    [Fact]
    public async Task ARefusedRefreshFallsBackToTheStoredApiKey()
    {
        var store = new RecordingTokenProvider(Expired("a-stored-key"));
        var (api, _) = Api(store, HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");

        Assert.Equal(NexusCredentialSource.StoredApiKey, await api.CredentialSource());
        Assert.Equal(0, store.Writes);
        Assert.Equal(RefreshToken, store.Stored.OAuth!.RefreshToken);
    }

    /// <summary>An expired login with no way to refresh it is not worth a request.</summary>
    [Fact]
    public async Task AnExpiredLoginWithNoRefreshTokenIsNotRefreshedOrStored()
    {
        var state = Expired();
        state.OAuth!.RefreshToken = null;
        var store = new RecordingTokenProvider(state);
        var (api, endpoint) = Api(store, HttpStatusCode.OK, "{\"access_token\":\"should-not-be-asked-for\"}");

        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, endpoint.Requests);
    }

    /// <summary>The other side of it: a refresh that works is stored, and is a login.</summary>
    [Fact]
    public async Task ARefreshThatWorksIsStored()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, _) = Api(store, HttpStatusCode.OK,
            "{\"access_token\":\"a-new-access-token\",\"refresh_token\":\"a-new-refresh-token\",\"expires_in\":3600}");

        Assert.Equal(NexusCredentialSource.OAuth, await api.CredentialSource());
        Assert.Equal(1, store.Writes);
        Assert.Equal("a-new-access-token", store.Stored.OAuth!.AccessToken);
        Assert.False(store.Stored.OAuth.IsExpired);
    }

    /// <summary>
    ///     A refusal stands for a while. Every authenticated call refreshes an expired token on its way out,
    ///     so retrying a doomed refresh each time would burn a round-trip per request - an install's worth of
    ///     them for a machine that is simply offline. The old behaviour hid this by writing the failure to
    ///     disk, which stopped the retries by destroying the login.
    /// </summary>
    [Fact]
    public async Task ARefusedRefreshIsNotRetriedOnEveryCall()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, endpoint) = Api(store, HttpStatusCode.ServiceUnavailable, "down for maintenance");

        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());

        Assert.Equal(1, endpoint.Requests);
        Assert.Equal(0, store.Writes);
    }

    /// <summary>And it is only a while: the user who plugs the network back in is not held to it.</summary>
    [Fact]
    public async Task ARefusedRefreshIsRetriedOnceTheWindowHasPassed()
    {
        var store = new RecordingTokenProvider(Expired());
        var (api, endpoint) = ImpatientApi(store,
            (HttpStatusCode.ServiceUnavailable, "down for maintenance"),
            (HttpStatusCode.OK, "{\"access_token\":\"a-new-access-token\",\"expires_in\":3600}"));

        Assert.Equal(NexusCredentialSource.None, await api.CredentialSource());
        Assert.Equal(NexusCredentialSource.OAuth, await api.CredentialSource());

        Assert.Equal(2, endpoint.Requests);
        Assert.Equal(1, store.Writes);
        Assert.Equal("a-new-access-token", store.Stored.OAuth!.AccessToken);
    }

    private static NexusOAuthState Expired(string apiKey = "")
    {
        return new NexusOAuthState
        {
            ApiKey = apiKey,
            OAuth = new JwtTokenReply
            {
                AccessToken = "an-access-token-past-its-time",
                RefreshToken = RefreshToken,
                // Received an hour ago and good for a minute: expired, and refreshable.
                ReceivedAt = DateTime.UtcNow.AddHours(-1).ToFileTimeUtc(),
                ExpiresIn = 60
            }
        };
    }

    /// <summary>
    ///     An API whose only way out is <paramref name="status" /> and <paramref name="body" />. The handler
    ///     comes back with it so a test can say how many requests were made, "none" included.
    /// </summary>
    private static (NexusApi Api, Endpoint Endpoint) Api(ITokenProvider<NexusOAuthState> store,
        HttpStatusCode status, string body)
    {
        var endpoint = new Endpoint((status, body));
        return (new NexusApi(store, NullLogger<NexusApi>.Instance, new HttpClient(endpoint),
            new Resource<HttpClient>("Test", 1), new ApplicationInfo(), new JsonSerializerOptions()), endpoint);
    }

    /// <summary>
    ///     The same, with a refresh that is willing to try again immediately. Waiting out the real window in
    ///     a test would mean sleeping a minute; what is under test is that the window is consulted at all,
    ///     which both this and <see cref="ARefusedRefreshIsNotRetriedOnEveryCall" /> pin from opposite sides.
    /// </summary>
    private static (NexusApi Api, Endpoint Endpoint) ImpatientApi(ITokenProvider<NexusOAuthState> store,
        params (HttpStatusCode Status, string Body)[] replies)
    {
        var endpoint = new Endpoint(replies);
        return (new NoRetryDelay(store, new HttpClient(endpoint)), endpoint);
    }

    private sealed class NoRetryDelay : NexusApi
    {
        public NoRetryDelay(ITokenProvider<NexusOAuthState> store, HttpClient client) : base(store,
            NullLogger<NexusApi>.Instance, client, new Resource<HttpClient>("Test", 1), new ApplicationInfo(),
            new JsonSerializerOptions())
        {
        }

        protected override TimeSpan RefreshRetryDelay => TimeSpan.Zero;
    }

    /// <summary>
    ///     Answers the only request this API makes without a token: the refresh. Replies are handed out in
    ///     order and the last one repeats, so a test says only as much as it cares about.
    /// </summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _replies;
        private (HttpStatusCode Status, string Body) _current;

        public Endpoint(params (HttpStatusCode Status, string Body)[] replies)
        {
            _replies = new Queue<(HttpStatusCode, string)>(replies);
            _current = replies[0];
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("https://users.nexusmods.com/oauth/token", request.RequestUri!.ToString());
            if (_replies.Count > 0) _current = _replies.Dequeue();
            return Task.FromResult(new HttpResponseMessage(_current.Status)
                {Content = new StringContent(_current.Body)});
        }
    }

    /// <summary>A stored login that remembers whether anything wrote to it.</summary>
    private sealed class RecordingTokenProvider : ITokenProvider<NexusOAuthState>
    {
        public RecordingTokenProvider(NexusOAuthState state)
        {
            Stored = state;
        }

        public NexusOAuthState Stored { get; private set; }
        public int Writes { get; private set; }

        public ValueTask<NexusOAuthState?> Get()
        {
            return ValueTask.FromResult<NexusOAuthState?>(Stored);
        }

        public ValueTask SetToken(NexusOAuthState val)
        {
            Writes++;
            Stored = val;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> Delete()
        {
            return ValueTask.FromResult(true);
        }

        public bool HaveToken()
        {
            return true;
        }
    }
}
