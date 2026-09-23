#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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
    ///     The redirect URI is built from <see cref="OAuthLoopbackListener.CallbackHost" /> rather than from
    ///     a spelling written out here, because which spelling is right is the authorization server's to say
    ///     and not ours: Nexus Mods matches a redirect URI against a per-client list as a string, so
    ///     <c>localhost</c> and <c>127.0.0.1</c> are two entries for one address and only the registered one
    ///     is accepted. Asserting the constant keeps this honest when that spelling is changed, while still
    ///     pinning the shape around it - scheme, the listening port, and the callback path.
    /// </summary>
    [Fact]
    public void TheRedirectUriIsLoopbackOnTheListeningPort()
    {
        using var listener = new OAuthLoopbackListener(Path);

        Assert.Equal($"http://{OAuthLoopbackListener.CallbackHost}:{listener.Port}/oauth/callback",
            listener.RedirectUri.ToString());
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
    ///     Browsers open speculative connections and leave them idle without ever sending a request. One of
    ///     those must not be able to stall the login: served one at a time with no clock on the read, an
    ///     idle socket sat in <c>ReadAsync</c> for ever while the redirect queued behind it, connected but
    ///     never accepted - a login that said "authorization successful" in the browser and never came back
    ///     to the app.
    /// </summary>
    [Fact]
    public async Task AConnectionThatSaysNothingDoesNotStallTheCallback()
    {
        using var listener = new OAuthLoopbackListener(Path);
        var waiting = listener.WaitForCallback(CancellationToken.None);

        // Connected and deliberately silent, exactly as a preconnect is, and held open throughout.
        using var silent = new TcpClient();
        await silent.ConnectAsync(IPAddress.Loopback, listener.Port);

        using var client = new HttpClient();
        var fetching = client.GetStringAsync($"{listener.RedirectUri}?code=a-code");

        using var callback = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("a-code", callback.Query["code"]);

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
    // The ordinary relative form is the case that broke: on Unix a target starting with "/" parses as an
    // absolute file path, so reading it as an absolute URI swallowed the query into an escaped path and no
    // redirect ever matched the callback. Only http and https are honoured as the absolute form now, which
    // also means a request line naming some other scheme is left as an opaque path that matches nothing
    // rather than being interpreted.
    [InlineData("GET /oauth/callback?code=a-code&state=s HTTP/1.1", "/oauth/callback", "a-code")]
    [InlineData("GET file:///etc/passwd HTTP/1.1", "file:///etc/passwd", null)]
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
