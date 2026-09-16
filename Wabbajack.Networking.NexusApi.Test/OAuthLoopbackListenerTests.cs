#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Networking.NexusApi.OAuth;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     The loopback server the OAuth redirect comes back to. Everything here is a socket on this machine,
///     so none of it needs the network or a login.
/// </summary>
public class OAuthLoopbackListenerTests
{
    private const string Path = "/oauth/callback";

    /// <summary>
    ///     The port is whatever the kernel had free, so it is never hardcoded and two listeners never
    ///     collide. Binding at all is the other half of this: Wabbajack must never need administrator
    ///     rights to log in, and a loopback socket does not - which is exactly what an <c>HttpListener</c>
    ///     prefix would have had to be argued about.
    /// </summary>
    [Fact]
    public void EachListenerGetsItsOwnFreePort()
    {
        using var first = new OAuthLoopbackListener(Path);
        using var second = new OAuthLoopbackListener(Path);

        Assert.InRange(first.Port, 1, 65535);
        Assert.InRange(second.Port, 1, 65535);
        Assert.NotEqual(first.Port, second.Port);
    }

    /// <summary>
    ///     Nexus Mods has <c>http://localhost:*/oauth/callback</c> registered, and a redirect URI is matched
    ///     as a string. 127.0.0.1 is the same address spelled differently and would be refused.
    /// </summary>
    [Fact]
    public void TheRedirectUriSpellsLocalhost()
    {
        using var listener = new OAuthLoopbackListener(Path);

        Assert.Equal($"http://localhost:{listener.Port}/oauth/callback", listener.RedirectUri.ToString());
    }

    [Fact]
    public async Task TheCallbackIsServedAndItsQueryIsRead()
    {
        using var listener = new OAuthLoopbackListener(Path);
        var waiting = listener.WaitForCallback(CancellationToken.None);

        using var client = new HttpClient();
        var fetching = client.GetStringAsync($"{listener.RedirectUri}?code=a-code&state=a-state");

        using var callback = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("a-code", callback.Query["code"]);
        Assert.Equal("a-state", callback.Query["state"]);

        await callback.Respond("<html><body>done</body></html>", CancellationToken.None);
        Assert.Contains("done", await fetching.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    ///     Exactly one path is served. A browser asking for /favicon.ico while the user is looking at the
    ///     page must not be mistaken for the redirect, and must not consume the wait either.
    /// </summary>
    [Fact]
    public async Task AnythingButTheCallbackPathIsRefusedAndTheWaitCarriesOn()
    {
        using var listener = new OAuthLoopbackListener(Path);
        var waiting = listener.WaitForCallback(CancellationToken.None);

        using var client = new HttpClient();
        var refused = await client.GetAsync($"http://localhost:{listener.Port}/favicon.ico");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.False(waiting.IsCompleted);

        var fetching = client.GetStringAsync($"{listener.RedirectUri}?code=a-code");
        using var callback = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("a-code", callback.Query["code"]);

        await callback.Respond("<html><body>done</body></html>", CancellationToken.None);
        await fetching.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    ///     Both loopback stacks are bound, because "localhost" resolves to ::1 as well as 127.0.0.1 and the
    ///     browser picks. A machine with no IPv6 loses nothing, so this only asserts the stack it has.
    /// </summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task EitherLoopbackAddressReachesIt(string address)
    {
        using var listener = new OAuthLoopbackListener(Path);
        var waiting = listener.WaitForCallback(CancellationToken.None);

        using var client = new HttpClient();
        var fetching = client.GetStringAsync($"http://{address}:{listener.Port}/oauth/callback?code=a-code");

        // A machine with that stack turned off refuses the connection, which is the fetch failing rather
        // than the wait ending. That is a pass: the other address is still bound and the browser will use
        // it.
        if (await Task.WhenAny(waiting, fetching) == fetching)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => fetching);
            return;
        }

        using var callback = await waiting;
        Assert.Equal("a-code", callback.Query["code"]);

        await callback.Respond("<html><body>done</body></html>", CancellationToken.None);
        await fetching.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WaitingIsGivenUpWhenTheCallerCancels()
    {
        using var listener = new OAuthLoopbackListener(Path);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.WaitForCallback(cancel.Token));
    }

    [Theory]
    [InlineData("GET /oauth/callback?code=a-code HTTP/1.1", "/oauth/callback", "a-code")]
    [InlineData("GET /oauth/callback HTTP/1.1", "/oauth/callback", null)]
    [InlineData("GET http://localhost:1234/oauth/callback?code=a-code HTTP/1.1", "/oauth/callback", "a-code")]
    public void ARequestLineIsSplitIntoItsPathAndQuery(string line, string path, string? code)
    {
        var target = OAuthLoopbackListener.ParseRequestLine(line);

        Assert.NotNull(target);
        Assert.Equal(path, target!.Path);
        target.Query.TryGetValue("code", out var found);
        Assert.Equal(code, found);
    }

    [Theory]
    [InlineData("POST /oauth/callback HTTP/1.1")]
    [InlineData("GET")]
    [InlineData("")]
    public void AnythingElseIsNotARequestLineThisServerAnswers(string line)
    {
        Assert.Null(OAuthLoopbackListener.ParseRequestLine(line));
    }
}
