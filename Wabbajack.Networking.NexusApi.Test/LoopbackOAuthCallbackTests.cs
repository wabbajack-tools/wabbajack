#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     The socket the OAuth redirect comes back to. It is the only thing this app listens on, so what it
///     answers, what it ignores and what it is reachable from are all pinned here; the flow around it does
///     nothing until this hands it a query.
/// </summary>
public class LoopbackOAuthCallbackTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     One port, chosen by the OS, on the loopback address. The port is not known before the listener
    ///     exists, which is why the redirect is read off it rather than written down anywhere.
    /// </summary>
    [Fact]
    public void TheRedirectNamesThePortThatWasOpened()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);

        Assert.InRange(callback.Port, 1, 65535);
        Assert.Equal($"http://127.0.0.1:{callback.Port}/oauth/callback", callback.RedirectUri);
    }

    [Fact]
    public async Task TheRedirectsQueryIsWhatComesBack()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);
        using var cancel = new CancellationTokenSource(Patience);

        var waiting = callback.WaitForRedirect(cancel.Token);
        var response = await Get(callback, "/oauth/callback?code=the%2Bcode&state=the-state", cancel.Token);

        var query = await waiting;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("the+code", query["code"]);
        Assert.Equal("the-state", query["state"]);
    }

    /// <summary>
    ///     An error is a redirect like any other: the flow is the one that decides what to say about it, and
    ///     it cannot do that if this is still waiting for a code that is never coming.
    /// </summary>
    [Fact]
    public async Task ARefusalComesBackToo()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);
        using var cancel = new CancellationTokenSource(Patience);

        var waiting = callback.WaitForRedirect(cancel.Token);
        await Get(callback, "/oauth/callback?error=access_denied&state=the-state", cancel.Token);

        Assert.Equal("access_denied", (await waiting)["error"]);
    }

    /// <summary>
    ///     Browsers ask for things nobody offered them - a favicon, most of all - and something that is not
    ///     the callback must not end the login. The port stays open and the real redirect is still received.
    /// </summary>
    [Fact]
    public async Task AnythingButTheCallbackPathIsIgnored()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);
        using var cancel = new CancellationTokenSource(Patience);

        var waiting = callback.WaitForRedirect(cancel.Token);

        var favicon = await Get(callback, "/favicon.ico", cancel.Token);
        var elsewhere = await Get(callback, "/?code=not-the-callback&state=the-state", cancel.Token);

        Assert.Equal(HttpStatusCode.NotFound, favicon.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
        Assert.False(waiting.IsCompleted);

        await Get(callback, "/oauth/callback?code=the-code&state=the-state", cancel.Token);
        Assert.Equal("the-code", (await waiting)["code"]);
    }

    /// <summary>
    ///     A connection that says nothing is what a browser's speculative one looks like. Handled on its
    ///     own, so it cannot hold the port while the real redirect is waiting behind it.
    /// </summary>
    [Fact]
    public async Task AConnectionThatSaysNothingDoesNotHoldThePort()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);
        using var cancel = new CancellationTokenSource(Patience);

        var waiting = callback.WaitForRedirect(cancel.Token);

        using var silent = new TcpClient();
        await silent.ConnectAsync(IPAddress.Loopback, callback.Port, cancel.Token);

        await Get(callback, "/oauth/callback?code=the-code&state=the-state", cancel.Token);

        Assert.Equal("the-code", (await waiting)["code"]);
    }

    /// <summary>
    ///     Cancellation is how a login that nobody finished ends, and the port has to go with it: a socket
    ///     left open for the rest of the session is the thing this was built not to do.
    /// </summary>
    [Fact]
    public async Task CancellingClosesThePort()
    {
        var callback = new LoopbackOAuthCallback(NullLogger.Instance);
        using var cancel = new CancellationTokenSource();

        var waiting = callback.WaitForRedirect(cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);

        callback.Dispose();

        using var refused = new TcpClient();
        await Assert.ThrowsAsync<SocketException>(async () =>
            await refused.ConnectAsync(IPAddress.Loopback, callback.Port, CancellationToken.None));
    }

    /// <summary>
    ///     Only the loopback address is bound, so nothing off this machine can reach the port. Checked by
    ///     asking the OS what is listening rather than by dialing in from elsewhere, which a test has no way
    ///     to do.
    /// </summary>
    [Fact]
    public void NothingIsBoundOffThisMachine()
    {
        using var callback = new LoopbackOAuthCallback(NullLogger.Instance);

        var listening = IPGlobalPropertiesListeners(callback.Port);

        Assert.NotEmpty(listening);
        Assert.All(listening, endpoint => Assert.True(IPAddress.IsLoopback(endpoint.Address),
            $"The callback port is bound to {endpoint.Address}, which is not loopback"));
    }

    private static IPEndPoint[] IPGlobalPropertiesListeners(int port)
    {
        return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Where(endpoint => endpoint.Port == port)
            .ToArray();
    }

    private static async Task<HttpResponseMessage> Get(LoopbackOAuthCallback callback, string path,
        CancellationToken token)
    {
        using var client = new HttpClient();
        return await client.GetAsync($"http://127.0.0.1:{callback.Port}{path}", token);
    }
}
