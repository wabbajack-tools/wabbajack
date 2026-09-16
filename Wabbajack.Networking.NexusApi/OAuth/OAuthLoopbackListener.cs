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

        // Spelled "localhost", deliberately. The callback registered with Nexus Mods is
        // http://localhost:*/oauth/callback, and a redirect URI is matched as a string: 127.0.0.1 is the
        // same address and a different string, and is refused.
        RedirectUri = new Uri($"http://localhost:{Port}{path}");
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
        while (true)
        {
            var client = await AcceptAny(token);
            var keep = false;
            try
            {
                var target = await ReadTarget(client, token);
                if (target == null)
                {
                    await WriteResponse(client, "400 Bad Request", null, token);
                    continue;
                }

                if (!string.Equals(target.Path, _path, StringComparison.Ordinal))
                {
                    await WriteResponse(client, "404 Not Found", null, token);
                    continue;
                }

                keep = true;
                return new OAuthCallback(client, target.Query);
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                // A connection that broke before it said anything the server understood is not the redirect;
                // the browser is still out there and the one that matters has not arrived yet.
            }
            finally
            {
                if (!keep) client.Dispose();
            }
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

        if (Uri.TryCreate(target, UriKind.Absolute, out var absolute))
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
