using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wabbajack.CLI.OAuth;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.CLI.Test;

public class NexusLoginTests : IDisposable
{
    private readonly string _testKeyName;
    private readonly AbsolutePath _encryptedPath;

    public NexusLoginTests()
    {
        _testKeyName = "test-nexus-" + Guid.NewGuid().ToString("N")[..8];
        _encryptedPath = KnownFolders.WabbajackAppLocal
            .Combine("encrypted")
            .Combine(_testKeyName.ToRelativePath());
    }

    public void Dispose()
    {
        if (_encryptedPath.FileExists()) _encryptedPath.Delete();
    }

    [Fact]
    public async Task Run_StoresOAuthToken_WhenFlowSucceeds()
    {
        var provider = MakeProvider();
        var flow = Substitute.For<INexusOAuthFlow>();
        flow.GetAuthorizationCode(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("fake-code-123");
        var fakeJwt = new JwtTokenReply { AccessToken = "token-abc", RefreshToken = "refresh-xyz", ExpiresIn = 3600 };
        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, provider, FakeHttpClient(fakeJwt), flow);

        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(0, result);
        Assert.True(provider.HaveToken());
        var stored = await provider.Get();
        Assert.NotNull(stored!.OAuth);
        Assert.Equal("token-abc", stored.OAuth!.AccessToken);
        Assert.True(string.IsNullOrEmpty(stored.ApiKey));
    }

    [Fact]
    public async Task Run_ReturnsOne_WhenFlowReturnsNull()
    {
        var provider = MakeProvider();
        var flow = Substitute.For<INexusOAuthFlow>();
        flow.GetAuthorizationCode(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, provider, FakeHttpClient(null), flow);

        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(1, result);
        Assert.False(provider.HaveToken());
    }

    [Fact]
    public async Task Run_ReturnsOne_WhenTokenExchangeFails()
    {
        var provider = MakeProvider();
        var flow = Substitute.For<INexusOAuthFlow>();
        flow.GetAuthorizationCode(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("some-code");
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = "Unauthorized" }));
        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, provider, httpClient, flow);

        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(1, result);
        Assert.False(provider.HaveToken());
    }

    [Fact]
    public void BuildAuthorizeUrl_ContainsPkceParameters()
    {
        var url = NexusLogin.BuildAuthorizeUrl("challenge123", "state456");
        var query = url.Query;

        Assert.Contains("code_challenge=challenge123", query);
        Assert.Contains("state=state456", query);
        Assert.Contains("code_challenge_method=S256", query);
        Assert.Contains("response_type=code", query);
        Assert.Contains("client_id=wabbajack", query);
    }

    [Fact]
    public void BuildAuthorizeUrl_UrlEncodesRedirectUri()
    {
        var url = NexusLogin.BuildAuthorizeUrl("challenge", "state");
        Assert.Contains("redirect_uri=", url.Query);
        Assert.DoesNotContain("redirect_uri=https://127", url.Query);
        Assert.Contains("redirect_uri=https%3A%2F%2F127.0.0.1%3A1234", url.Query);
    }

    [Fact]
    public async Task ExchangeCode_PassesVerifierAndCodeToServer()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new JwtTokenReply { AccessToken = "t" }),
                    Encoding.UTF8, "application/json")
            };
        });

        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, MakeProvider(),
            new HttpClient(handler), Substitute.For<INexusOAuthFlow>());

        await verb.ExchangeCode("my-verifier", "my-code", CancellationToken.None);

        Assert.NotNull(captured);
        var body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("code_verifier=my-verifier", body);
        Assert.Contains("code=my-code", body);
    }

    private EncryptedJsonTokenProvider<NexusOAuthState> MakeProvider() =>
        new(NullLogger.Instance, null!, _testKeyName);

    private static HttpClient FakeHttpClient(JwtTokenReply? reply) =>
        new(new FakeHttpMessageHandler(_ => reply == null
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(reply), Encoding.UTF8, "application/json")
            }));

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_handler(request));
    }
}
