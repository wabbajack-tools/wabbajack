#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Networking.NexusApi.OAuth;

/// <summary>
///     The request line of an HTTP request, split into the only two things this server cares about.
/// </summary>
public sealed record OAuthRequestTarget(string Path, IReadOnlyDictionary<string, string> Query);

/// <summary>
///     A one-shot HTTP server on the loopback interface, which is how RFC 8252 says a desktop app should
///     receive an OAuth redirect: the browser is the real one the user already trusts, and the code comes
///     back over a socket only this machine can reach.
///     <para>
///         This is a raw <see cref="TcpListener" /> rather than an <c>HttpListener</c>, for two reasons.
///         HttpListener hands the port to http.sys, which listens on the wildcard address even for a
///         <c>http://localhost:port/</c> prefix - a machine elsewhere on the network can open a TCP
///         connection to it and is turned away only by http.sys comparing the Host header - so the socket
///         itself is not loopback-only. And HttpListener cannot be asked for a free port, since it takes a
///         prefix with the port already written into it, so the port would have to be probed with a socket
///         that is then closed, leaving a window for something else to take it. A TcpListener bound to
///         <see cref="IPAddress.Loopback" /> on port 0 has neither problem. The cost is reading the request
///         line, which is the first line of the request and all this server ever needs.
///     </para>
///     <para>
///         Both loopback stacks are bound, because <c>localhost</c> resolves to <c>::1</c> as well as
///         <c>127.0.0.1</c> and a browser is free to try either - in practice Windows browsers try
///         <c>::1</c> first. The IPv6 half is best effort: the port is whatever the IPv4 bind was given, so
///         a machine with IPv6 turned off, or one where that one port happens to be taken on <c>::1</c>
///         alone, carries on over IPv4 and loses nothing but a reconnect.
///     </para>
/// </summary>
public sealed class OAuthLoopbackListener : IDisposable
{
    /// <summary>
    ///     How much of a request is read looking for the end of the request line. A redirect carrying a code,
    ///     a state and an error description is a few hundred bytes; anything past this is not one, and the
    ///     limit is what stops a connection that never sends a newline from being read for ever.
    /// </summary>
    private const int MaxRequestLineBytes = 8 * 1024;

    /// <summary>
    ///     How long one connection gets to send its request line. A browser that has opened a socket and
    ///     means to use it sends immediately over loopback; one that has not said anything by now is a
    ///     speculative connection, and waiting on it indefinitely is what let a single idle socket stall the
    ///     login. Long enough that a slow machine is never cut off, short enough that the socket is not held
    ///     for the rest of the login.
    /// </summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     How the loopback address is spelled in the redirect URI. It is one character of difference that
    ///     the socket does not care about at all and the authorization server may care about entirely: Nexus
    ///     Mods keeps a server-side list of redirect URIs per client and matches against it as a string, so
    ///     <c>localhost</c> and <c>127.0.0.1</c> are the same machine and two different entries, and only
    ///     whichever one is actually on the list is accepted. The literal IP is tried first because the
    ///     client's previous registration was <c>https://127.0.0.1:1234</c>, so that spelling is the one
    ///     already known to exist.
    ///     <para>
    ///         Both loopback stacks are still bound, so this is only about what the browser is told to
    ///         visit. Spelled as the IPv4 literal, the browser goes straight to the IPv4 socket.
    ///     </para>
    /// </summary>
    public const string CallbackHost = "127.0.0.1";

    private readonly string _path;
    private readonly TcpListener _v4;
    private readonly TcpListener? _v6;

    private Task<TcpClient>? _pendingV4;
    private Task<TcpClient>? _pendingV6;

    /// <param name="path">The one path served, with its leading slash. Everything else gets a 404.</param>
    public OAuthLoopbackListener(string path)
    {
        _path = path;

        // Port 0 is the kernel choosing a free ephemeral port and handing it back already bound, so there is
        // no port to hardcode, no probe-then-bind window, and nothing to retry: by the time Port can be read
        // the socket owns it. The only way this fails is every ephemeral port on the machine being in use, in
        // which case the bind throws a SocketException and the login reports that it could not start.
        _v4 = new TcpListener(IPAddress.Loopback, 0);
        _v4.Start();
        Port = ((IPEndPoint) _v4.LocalEndpoint).Port;

        _v6 = TryBindIPv6(Port);

        RedirectUri = new Uri($"http://{CallbackHost}:{Port}{path}");
    }

    /// <summary>The loopback port this is listening on.</summary>
    public int Port { get; }

    /// <summary>
    ///     What to send as <c>redirect_uri</c>, and what the token exchange has to send back byte for byte.
    /// </summary>
    public Uri RedirectUri { get; }

    public void Dispose()
    {
        _v4.Dispose();
        _v6?.Dispose();

        // An accept still outstanding faults when its listener closes. Nothing is waiting on it by then, and
        // a faulted task nobody looks at is raised on the finalizer thread as an unobserved exception, so it
        // is looked at here and thrown away.
        Observe(_pendingV4);
        Observe(_pendingV6);
    }

    private static TcpListener? TryBindIPv6(int port)
    {
        if (!Socket.OSSupportsIPv6) return null;

        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.IPv6Loopback, port);
            listener.Start();
            return listener;
        }
        catch (SocketException)
        {
            listener?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///     Waits for a request to the callback path and hands it back with its connection still open, so the
    ///     caller can decide what page the user is shown before it is closed. Anything else that connects -
    ///     a favicon fetch, a port scanner, a browser prefetching - is answered and ignored.
    /// </summary>
    public async Task<OAuthCallback> WaitForCallback(CancellationToken token)
    {
        var found = new TaskCompletionSource<OAuthCallback>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var giveUp = token.Register(() => found.TrySetCanceled(token));

        using var accepting = CancellationTokenSource.CreateLinkedTokenSource(token);

        // Accepting runs on its own, so the next connection is taken while the last one is still being read.
        // Each connection is then served independently, which is the whole point: a socket that connects and
        // says nothing must not be able to hold up the one that matters.
        var loop = Task.Run(async () =>
        {
            try
            {
                while (!accepting.IsCancellationRequested)
                {
                    var client = await AcceptAny(accepting.Token);
                    _ = Serve(client, found, accepting.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException
                                           or SocketException)
            {
                // The listener closed, or the login ended. Either way there is nothing left to accept for.
            }
            catch (Exception ex)
            {
                found.TrySetException(ex);
            }
        }, CancellationToken.None);

        try
        {
            return await found.Task;
        }
        finally
        {
            accepting.Cancel();
            await loop.ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reads one connection and either hands it back as the callback or answers and closes it.
    ///     <para>
    ///         The read is bounded by <see cref="ReadTimeout" /> because nothing obliges a connection to say
    ///         anything. Browsers open speculative connections and hold them idle, and this server used to
    ///         read them one at a time with no clock on it: a silent socket sat in <c>ReadAsync</c> for ever
    ///         while the redirect that mattered queued behind it, connected but never accepted. What that
    ///         looked like was a login that reached "authorization successful" in the browser and then never
    ///         came back to the app.
    ///     </para>
    /// </summary>
    private async Task Serve(TcpClient client, TaskCompletionSource<OAuthCallback> found, CancellationToken token)
    {
        var keep = false;
        try
        {
            using var reading = CancellationTokenSource.CreateLinkedTokenSource(token);
            reading.CancelAfter(ReadTimeout);

            var target = await ReadTarget(client, reading.Token);
            if (target == null)
            {
                await WriteResponse(client, "400 Bad Request", null, token);
                return;
            }

            if (!string.Equals(target.Path, _path, StringComparison.Ordinal))
            {
                await WriteResponse(client, "404 Not Found", null, token);
                return;
            }

            var callback = new OAuthCallback(client, target.Query);

            // Whoever gets there first is the login. A second redirect - a refreshed tab, a browser
            // replaying it - has nobody waiting for it, so it is closed rather than leaked.
            if (found.TrySetResult(callback)) keep = true;
            else callback.Dispose();
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException
                                       or ObjectDisposedException)
        {
            // A connection that broke, said nothing, or ran out of time is not the redirect. The browser is
            // still out there and the one that matters has not arrived yet.
        }
        finally
        {
            if (!keep) client.Dispose();
        }
    }

    /// <summary>
    ///     Whichever loopback stack the browser used. Both accepts are kept across iterations, so a favicon
    ///     fetch answered on one does not lose the redirect arriving on the other.
    /// </summary>
    private async Task<TcpClient> AcceptAny(CancellationToken token)
    {
        _pendingV4 ??= _v4.AcceptTcpClientAsync(token).AsTask();
        if (_v6 != null) _pendingV6 ??= _v6.AcceptTcpClientAsync(token).AsTask();

        var pending = _pendingV6 == null
            ? new[] {_pendingV4}
            : new[] {_pendingV4, _pendingV6};

        var done = await Task.WhenAny(pending);
        if (done == _pendingV4) _pendingV4 = null;
        else _pendingV6 = null;

        return await done;
    }

    private static async Task<OAuthRequestTarget?> ReadTarget(TcpClient client, CancellationToken token)
    {
        var stream = client.GetStream();
        var buffer = new byte[MaxRequestLineBytes];
        var read = 0;

        while (read < buffer.Length)
        {
            var got = await stream.ReadAsync(buffer.AsMemory(read), token);
            if (got == 0) return null;

            var start = Math.Max(0, read - 1);
            read += got;

            var newline = Array.IndexOf(buffer, (byte) '\n', start, read - start);
            if (newline < 0) continue;

            var line = Encoding.ASCII.GetString(buffer, 0, newline).TrimEnd('\r');
            return ParseRequestLine(line);
        }

        return null;
    }

    /// <summary>
    ///     The path and query out of an HTTP request line - <c>GET /oauth/callback?code=... HTTP/1.1</c> - or
    ///     null for anything that is not a GET of something this server could serve.
    ///     <para>
    ///         The absolute form, <c>GET http://localhost:1234/oauth/callback?... HTTP/1.1</c>, is accepted
    ///         too: browsers only send it to a proxy, but it is a legal request line and refusing it would
    ///         turn a user's proxy setting into a login that never comes back.
    ///     </para>
    /// </summary>
    public static OAuthRequestTarget? ParseRequestLine(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length < 2) return null;
        if (!string.Equals(parts[0], "GET", StringComparison.Ordinal)) return null;

        var target = parts[1];
        if (target.Length == 0) return null;

        // Absolute *and* actually a web address. "Absolute" alone is not the same question on every
        // platform: on Unix a target beginning with "/" - which is what every ordinary request line carries
        // - parses as an absolute file path, so this matched, and "/oauth/callback?code=..." came back as a
        // path of "/oauth/callback%3Fcode=..." with the query swallowed and escaped into it. The callback
        // path then never matched and every redirect was refused. The absolute form of a request line is
        // only ever http or https, so asking for that is both the narrower question and the right one.
        if (Uri.TryCreate(target, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return new OAuthRequestTarget(absolute.AbsolutePath, OAuthQuery.Parse(absolute.Query));

        var split = target.IndexOf('?');
        var path = split < 0 ? target : target[..split];
        var query = split < 0 ? string.Empty : target[(split + 1)..];

        return new OAuthRequestTarget(path, OAuthQuery.Parse(query));
    }

    internal static async Task WriteResponse(TcpClient client, string status, string? html, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(html ?? string.Empty);
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        var stream = client.GetStream();
        await stream.WriteAsync(head, token);
        await stream.WriteAsync(body, token);
        await stream.FlushAsync(token);

        // Half-close rather than letting Dispose reset the connection, so the page the user is looking at is
        // the one that was written rather than a browser error.
        try
        {
            client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (SocketException)
        {
            // Already gone. The browser has whatever it managed to read.
        }
    }

    private static void Observe(Task? task)
    {
        task?.ContinueWith(t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

/// <summary>
///     The redirect, with the browser still on the other end of it: read <see cref="Query" />, decide what
///     happened, then <see cref="Respond" /> with the page that says so.
/// </summary>
public sealed class OAuthCallback : IDisposable
{
    private readonly TcpClient _client;

    internal OAuthCallback(TcpClient client, IReadOnlyDictionary<string, string> query)
    {
        _client = client;
        Query = query;
    }

    public IReadOnlyDictionary<string, string> Query { get; }

    public void Dispose()
    {
        _client.Dispose();
    }

    public Task Respond(string html, CancellationToken token)
    {
        return OAuthLoopbackListener.WriteResponse(_client, "200 OK", html, token);
    }
}
