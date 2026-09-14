#nullable enable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi.DTOs;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Preflight;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     Pins the one definition of being logged in to Nexus Mods across the two places that used to have their
///     own. <c>NexusDownloader.Prepare</c> gates downloads; <c>NexusApiLoginProbe</c> tells preflight what to
///     show the user. They disagreed over <c>NEXUS_API_KEY</c>: the API validates with it, the downloader
///     cannot use it, and the checklist reported a premium login while every Nexus archive was being routed to
///     the browser. Every combination below is checked both ways round, so the split cannot come back.
///     <para>
///         These tests move the process' own <c>NEXUS_API_KEY</c>, which is how the CLI and the RequiresOAuth
///         tests authenticate, so they share a collection with <see cref="NexusApiTests" /> rather than running
///         beside it.
///     </para>
/// </summary>
[Collection(NexusCredentialTests.SerialisedAroundTheApiKey)]
public class NexusCredentialTests : IDisposable
{
    public const string SerialisedAroundTheApiKey = "NEXUS_API_KEY";
    private const string EnvironmentKey = "NEXUS_API_KEY";

    private readonly string? _originalKey = Environment.GetEnvironmentVariable(EnvironmentKey);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentKey, _originalKey);
    }

    public static TheoryData<bool, bool, bool, NexusCredentialSource> Cases()
    {
        return new TheoryData<bool, bool, bool, NexusCredentialSource>
        {
            // stored OAuth, stored API key, NEXUS_API_KEY set, what the API would use
            {false, false, false, NexusCredentialSource.None},
            {false, false, true, NexusCredentialSource.EnvironmentApiKey},
            {true, false, false, NexusCredentialSource.OAuth},
            {true, false, true, NexusCredentialSource.OAuth},
            {false, true, false, NexusCredentialSource.StoredApiKey},
            {false, true, true, NexusCredentialSource.StoredApiKey},
            // A stored login carrying both: OAuth is what the API reaches for.
            {true, true, true, NexusCredentialSource.OAuth}
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheApiReportsTheCredentialItWouldUse(bool oauth, bool storedKey, bool environmentKey,
        NexusCredentialSource expected)
    {
        var api = Api(oauth, storedKey, environmentKey);

        Assert.Equal(expected, await api.CredentialSource());
    }

    /// <summary>
    ///     The property that matters: what preflight calls a login and what the downloader will act on are the
    ///     same answer, in every combination, without either side testing the token provider for itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheProbeAndTheDownloaderAgree(bool oauth, bool storedKey, bool environmentKey,
        NexusCredentialSource expected)
    {
        var api = Api(oauth, storedKey, environmentKey);
        var downloader = new NexusDownloader(NullLogger<NexusDownloader>.Instance, null!, api);
        var probe = new NexusApiLoginProbe(api);

        var prepared = await downloader.Prepare();
        var status = await probe.Probe(CancellationToken.None);

        Assert.Equal(expected.CanDownload(), prepared);
        Assert.Equal(prepared, status.HasToken);
        Assert.Equal(expected, status.Credential);
    }

    /// <summary>
    ///     The reported bug, stated as a test: a key in the environment and nothing stored is not a login, and
    ///     the probe says so even though the same key validates happily against the API.
    /// </summary>
    [Fact]
    public async Task AnEnvironmentApiKeyAloneIsNotALogin()
    {
        var api = Api(false, false, true);

        var status = await new NexusApiLoginProbe(api).Probe(CancellationToken.None);

        Assert.False(status.HasToken);
        Assert.False(status.LoggedIn);
        Assert.False(status.IsPremium);
        Assert.Equal(NexusCredentialSource.EnvironmentApiKey, status.Credential);
        Assert.Equal(0, api.Validations);
    }

    [Fact]
    public async Task AStoredLoginIsProbedAgainstTheApi()
    {
        var api = Api(true, false, false);

        var status = await new NexusApiLoginProbe(api).Probe(CancellationToken.None);

        Assert.True(status.HasToken);
        Assert.True(status.LoggedIn);
        Assert.True(status.IsPremium);
        Assert.Equal("someone", status.UserName);
        Assert.Equal(NexusCredentialSource.OAuth, status.Credential);
    }

    [Theory]
    [InlineData(NexusCredentialSource.None, false)]
    [InlineData(NexusCredentialSource.EnvironmentApiKey, false)]
    [InlineData(NexusCredentialSource.OAuth, true)]
    [InlineData(NexusCredentialSource.StoredApiKey, true)]
    public void OnlyAStoredLoginCanDownload(NexusCredentialSource source, bool expected)
    {
        Assert.Equal(expected, source.CanDownload());
    }

    private TestApi Api(bool oauth, bool storedKey, bool environmentKey)
    {
        Environment.SetEnvironmentVariable(EnvironmentKey, environmentKey ? "an-api-key" : null);
        return new TestApi(new FakeTokenProvider(oauth, storedKey));
    }

    /// <summary>
    ///     Everything under test here is decided before a request goes out, so the one call that would leave
    ///     the machine is answered in place and counted.
    /// </summary>
    private sealed class TestApi : NexusApi
    {
        public TestApi(ITokenProvider<NexusOAuthState> authInfo) : base(authInfo,
            NullLogger<NexusApi>.Instance, new HttpClient(), new Resource<HttpClient>("Test", 1),
            new ApplicationInfo(), new JsonSerializerOptions())
        {
        }

        public int Validations { get; private set; }

        public override Task<(ValidateInfo info, ResponseMetadata header)> Validate(
            CancellationToken token = default)
        {
            Validations++;
            return Task.FromResult((new ValidateInfo {Name = "someone", IsPremium = true}, new ResponseMetadata()));
        }
    }

    private sealed class FakeTokenProvider : ITokenProvider<NexusOAuthState>
    {
        private readonly NexusOAuthState? _state;

        public FakeTokenProvider(bool oauth, bool storedKey)
        {
            if (!oauth && !storedKey) return;
            _state = new NexusOAuthState
            {
                // A token that is not about to expire: refreshing one is a network call, and nothing here is
                // testing the refresh.
                OAuth = oauth
                    ? new JwtTokenReply
                    {
                        AccessToken = "an-access-token",
                        ReceivedAt = DateTime.UtcNow.ToFileTimeUtc(),
                        ExpiresIn = 3600
                    }
                    : null,
                ApiKey = storedKey ? "a-stored-key" : string.Empty
            };
        }

        public ValueTask<NexusOAuthState?> Get()
        {
            if (_state == null) throw new Exception("No login data for nexus-oauth-info");
            return ValueTask.FromResult<NexusOAuthState?>(_state);
        }

        public ValueTask SetToken(NexusOAuthState val)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> Delete()
        {
            return ValueTask.FromResult(true);
        }

        public bool HaveToken()
        {
            return _state != null;
        }
    }
}
