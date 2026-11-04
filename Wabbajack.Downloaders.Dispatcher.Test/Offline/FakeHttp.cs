using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     An in-process stand-in for a web server: every request goes to <c>respond</c>, and is kept so a test
///     can see what was asked for. Nothing leaves the process. Doubles as the <see cref="IHttpClientFactory" />
///     the resumable downloader takes.
/// </summary>
public class FakeHttp : HttpMessageHandler, IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
    private int _count;

    /// <param name="respond">Given the request and how many came before it.</param>
    public FakeHttp(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((request, _) => respond(request))
    {
    }

    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    public HttpClient Client() => new(this, false);

    HttpClient IHttpClientFactory.CreateClient(string name) => Client();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests.Enqueue(request);
        var response = _respond(request, Interlocked.Increment(ref _count) - 1);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Ok(byte[] body, string mediaType = "application/octet-stream")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        // A real response carries the length from the wire; ByteArrayContent only works it out when asked.
        content.Headers.ContentLength = body.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) {Content = content};
    }

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "", string mediaType = "text/html")
    {
        var content = new StringContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(status) {Content = content};
    }

    /// <summary>
    ///     Answers a request the way a server that honours <c>Range</c> does: 206 with the rest of the file
    ///     from the requested offset, 416 past the end, and the whole file when no range was asked for.
    /// </summary>
    public static HttpResponseMessage Ranged(HttpRequestMessage request, byte[] file, int? failAfter = null)
    {
        var from = request.Headers.Range?.Ranges is { Count: > 0 } ranges ? ranges.First().From ?? 0 : 0;
        if (from >= file.Length && file.Length > 0)
            return Status(HttpStatusCode.RequestedRangeNotSatisfiable);

        var body = file.AsSpan((int) from).ToArray();
        HttpContent content = failAfter == null
            ? new ByteArrayContent(body)
            : new StreamContent(new FailingStream(body, failAfter.Value));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = body.Length;
        if (from > 0)
            content.Headers.ContentRange = new ContentRangeHeaderValue(from, file.Length - 1, file.Length);

        return new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) {Content = content};
    }

    /// <summary>A body that drops the connection after <c>failAfter</c> bytes, the way a flaky link does.</summary>
    public sealed class FailingStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _failAfter;
        private int _position;

        public FailingStream(byte[] data, int failAfter)
        {
            _data = data;
            _failAfter = failAfter;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (_position >= _failAfter)
                throw new IOException("The connection was reset.");
            var count = Math.Min(buffer.Length, Math.Min(_failAfter, _data.Length) - _position);
            _data.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }
    }
}
