using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Networking.NexusApi;

/// <summary>
///     The one thing this app listens on: a loopback socket the OAuth redirect comes back to. The login
///     happens in the user's own browser, so the authorization code arrives as an ordinary HTTP GET rather
///     than as a navigation this app can watch, and something has to be there to receive it.
///     <para>
///         The socket is bound to <see cref="IPAddress.Loopback" /> on port 0 - one port, chosen by the OS,
///         reachable from this machine and nowhere else. The port is therefore not known until the listener
///         exists, which is why <see cref="RedirectUri" /> is read off it rather than being a constant: it is
///         the redirect sent to Nexus Mods and the one repeated at the token exchange, and the two have to be
///         the same string.
///     </para>
///     <para>
///         <c>HttpListener</c> would be the shorter way to write this and is not used on purpose: it hands
///         the prefix to http.sys, which is a machine-wide registration with its own ACLs, and a failure
///         there is an elevation prompt rather than a bad login. A socket is what was asked for and is all
///         that is opened.
///     </para>
/// </summary>
public sealed class LoopbackOAuthCallback : IDisposable
{
    /// <summary>The only path answered. Anything else on this port is a 404, including a browser's favicon.</summary>
    public const string CallbackPath = "/oauth/callback";

    /// <summary>
    ///     How long one connection has to say what it wants. Browsers open speculative connections and send
    ///     nothing down them, so a connection that goes quiet must not hold the port: each is handled on its
    ///     own and dropped when it says nothing, while the listener keeps accepting.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A request line longer than this is not one we are waiting for.</summary>
    private const int MaxRequestLine = 8 * 1024;

    private readonly TcpListener _listener;
    private readonly ILogger _logger;

    private readonly TaskCompletionSource<IReadOnlyDictionary<string, string>> _redirect =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _disposed;

    public LoopbackOAuthCallback(ILogger logger)
    {
        _logger = logger;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
    }

    /// <summary>The loopback port the OS handed out, open for as long as this object is not disposed.</summary>
    public int Port { get; }

    /// <summary>
    ///     What Nexus Mods is asked to redirect to, and what the token exchange has to repeat verbatim.
    /// </summary>
    public string RedirectUri => $"http://127.0.0.1:{Port}{CallbackPath}";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _redirect.TrySetCanceled();
        _listener.Stop();
        _listener.Dispose();
    }

    /// <summary>
    ///     The query the redirect arrived with - the authorization code and the state, or the error Nexus
    ///     Mods sent instead. Cancellation is the only other way out: the user closed the browser, gave up,
    ///     or the caller timed the login out.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> WaitForRedirect(CancellationToken token)
    {
        await using var registration = token.Register(() => _redirect.TrySetCanceled(token));

        // Nothing awaits the accept loop: it ends on its own when the listener is stopped, and a login that
        // has its answer is not made to wait on a socket.
        _ = Task.Run(() => Accept(token), CancellationToken.None);

        return await _redirect.Task;
    }

    private async Task Accept(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !_redirect.Task.IsCompleted)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => Handle(client, token), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
            // Disposed while waiting for a connection, which is how a cancelled login ends.
        }
        catch (Exception ex)
        {
            _redirect.TrySetException(ex);
        }
    }

    private async Task Handle(TcpClient client, CancellationToken token)
    {
        using var held = client;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            await using var stream = client.GetStream();
            var target = RequestTarget(await ReadRequestLine(stream, timeout.Token));

            if (target == null || !IsCallback(target, out var query))
            {
                await Respond(stream, "404 Not Found", NotFoundPage, timeout.Token);
                await Farewell(client, stream, timeout.Token);
                return;
            }

            // Answered before the result is handed over, so the page the user is looking at is written
            // while this socket is certainly still open. The goodbye comes after the result rather than
            // before it, so a login is never waiting on the browser getting round to closing a socket.
            await Respond(stream, "200 OK", DonePage, timeout.Token);
            _redirect.TrySetResult(query);
            await Farewell(client, stream, timeout.Token);
        }
        catch (Exception ex)
        {
            // One connection failing is not the login failing - the browser may well try again on another.
            _logger.LogDebug(ex, "A connection to the OAuth callback port went nowhere");
        }
    }

    /// <summary>
    ///     Ends the connection politely: half-closes it, then reads until the other end has done the same.
    ///     Only the request line is ever read, so the rest of the request - the headers a browser sends
    ///     after it - is still sitting in the receive buffer, and closing a socket with unread bytes in it
    ///     is an abortive close. That is an RST, which the browser reports as a connection reset in place of
    ///     the page that was just written to it.
    /// </summary>
    private async Task Farewell(TcpClient client, Stream stream, CancellationToken token)
    {
        try
        {
            client.Client.Shutdown(SocketShutdown.Send);

            var scratch = new byte[1024];
            while (await stream.ReadAsync(scratch, token) > 0)
            {
            }
        }
        catch (Exception ex)
        {
            // Best effort: what was being said goodbye to is a socket that has already been answered.
            _logger.LogDebug(ex, "The OAuth callback connection did not close cleanly");
        }
    }

    private static async Task<string?> ReadRequestLine(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        var line = new StringBuilder();

        while (line.Length < MaxRequestLine)
        {
            if (await stream.ReadAsync(buffer.AsMemory(0, 1), token) == 0) return null;

            var ch = (char) buffer[0];
            if (ch == '\n') return line.ToString().TrimEnd('\r');
            line.Append(ch);
        }

        return null;
    }

    /// <summary>The request target of a GET, or null for anything else. Nothing here answers a POST.</summary>
    internal static string? RequestTarget(string? requestLine)
    {
        if (string.IsNullOrWhiteSpace(requestLine)) return null;

        var parts = requestLine.Split(' ');
        if (parts.Length < 2) return null;
        if (!parts[0].Equals("GET", StringComparison.Ordinal)) return null;

        return parts[1];
    }

    private static bool IsCallback(string target, out IReadOnlyDictionary<string, string> query)
    {
        query = new Dictionary<string, string>();

        var split = target.IndexOf('?');
        var path = split < 0 ? target : target[..split];
        if (!path.Equals(CallbackPath, StringComparison.Ordinal)) return false;

        query = ParseQuery(split < 0 ? string.Empty : target[(split + 1)..]);
        return true;
    }

    /// <summary>
    ///     The query string as pairs. Written out here rather than taken from a web framework because this
    ///     project has none, and what it has to read is the three parameters a redirect brought back.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=');
            var name = Decode(split < 0 ? pair : pair[..split]);
            var value = split < 0 ? string.Empty : Decode(pair[(split + 1)..]);

            // First wins: a repeated parameter is not something Nexus Mods sends, and taking the last one
            // would let whatever else reached this port append to what did.
            parsed.TryAdd(name, value);
        }

        return parsed;
    }

    private static string Decode(string value)
    {
        return Uri.UnescapeDataString(value.Replace("+", "%20"));
    }

    private static async Task Respond(Stream stream, string status, string body, CancellationToken token)
    {
        var content = Encoding.UTF8.GetBytes(body);
        var header = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(header, token);
        await stream.WriteAsync(content, token);
        await stream.FlushAsync(token);
    }

    private const string DonePage =
        "<html><head><title>Wabbajack</title></head>" +
        "<body style=\"font-family: sans-serif; text-align: center; padding-top: 4em\">" +
        "<h2>You are logged in to Nexus Mods.</h2>" +
        "<p>You can close this tab and go back to Wabbajack.</p>" +
        "</body></html>";

    private const string NotFoundPage =
        "<html><head><title>Wabbajack</title></head><body>Nothing here.</body></html>";
}
