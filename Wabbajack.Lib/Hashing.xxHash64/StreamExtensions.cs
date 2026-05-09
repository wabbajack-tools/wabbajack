using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.RateLimiter;

namespace Wabbajack.Hashing.xxHash64;

public static class StreamExtensions
{
    public static async Task<Hash> Hash(this Stream stream, CancellationToken token)
    {
        return await stream.HashingCopy(Stream.Null, token);
    }

    public static async Task<Hash> HashingCopy(this Stream inputStream, Stream outputStream,
        CancellationToken token, IJob? job = null, int buffserSize = 1024 * 1024)
    {
        using var rented = MemoryPool<byte>.Shared.Rent(buffserSize);
        var buffer = rented.Memory;

        var hasher = new XxHash64();

        var running = true;
        while (running && !token.IsCancellationRequested)
        {
            var totalRead = 0;

            while (totalRead != buffer.Length)
            {
                var read = await inputStream.ReadAsync(buffer.Slice(totalRead, buffer.Length - totalRead),
                    token);

                if (read == 0)
                {
                    running = false;
                    break;
                }

                if (job != null)
                    await job.Report(read, token);

                totalRead += read;
            }

            var pendingWrite = outputStream.WriteAsync(buffer[..totalRead], token);
            hasher.Append(buffer[..totalRead].Span);
            await pendingWrite;
        }

        await outputStream.FlushAsync(token);

        return new Hash(hasher.GetCurrentHashAsUInt64());
    }

    public static async Task<Hash> HashingCopy(this Stream inputStream, Func<Memory<byte>, Task> fn,
        CancellationToken token)
    {
        using var rented = MemoryPool<byte>.Shared.Rent(1024 * 1024);
        var buffer = rented.Memory;

        var hasher = new XxHash64();

        var running = true;
        while (running && !token.IsCancellationRequested)
        {
            var totalRead = 0;

            while (totalRead != buffer.Length)
            {
                var read = await inputStream.ReadAsync(buffer.Slice(totalRead, buffer.Length - totalRead),
                    token);

                if (read == 0)
                {
                    running = false;
                    break;
                }
                totalRead += read;
            }

            var pendingWrite = fn(buffer[..totalRead]);
            hasher.Append(buffer[..totalRead].Span);
            await pendingWrite;
        }

        return new Hash(hasher.GetCurrentHashAsUInt64());
    }

    public static (Stream InputStream, Func<Hash> Fn) HashingPull(this Stream src)
    {
        var stream = new PullingStream(src);
        return (new BufferedStream(stream), () => stream.Hash);
    }

    class PullingStream : Stream
    {
        private readonly Stream _src;
        private readonly XxHash64 _hasher;
        private bool _finished;

        public PullingStream(Stream src)
        {
            _src = src;
            _hasher = new XxHash64();
        }

        public Hash Hash
        {
            get
            {
                if (!_finished)
                    throw new InvalidOperationException("Hash not yet computed");
                return new Hash(_hasher.GetCurrentHashAsUInt64());
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _src.Length;
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_finished)
                throw new InvalidDataException("HashingPull can only be read once");

            var read = _src.Read(buffer, offset, count);

            if (read == 0)
            {
                _finished = true;
                return 0;
            }

            _hasher.Append(buffer.AsSpan(offset, read));
            return read;
        }
    }
}
