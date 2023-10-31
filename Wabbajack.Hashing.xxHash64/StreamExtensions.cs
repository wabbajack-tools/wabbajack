using System;
using System.Buffers;
using System.IO;
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

        var hasher = new xxHashAlgorithm(0);

        var running = true;
        ulong finalHash = 0;
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
            if (running)
            {
                hasher.TransformByteGroupsInternal(buffer.Span);
                await pendingWrite;
            }
            else
            {
                var preSize = (totalRead >> 5) << 5;
                if (preSize > 0)
                {
                    hasher.TransformByteGroupsInternal(buffer[..preSize].Span);
                    finalHash = hasher.FinalizeHashValueInternal(buffer[preSize..totalRead].Span);
                    await pendingWrite;
                    break;
                }

                finalHash = hasher.FinalizeHashValueInternal(buffer[..totalRead].Span);
                await pendingWrite;
                break;
            }
        }

        await outputStream.FlushAsync(token);

        return new Hash(finalHash);
    }
    
    public static async Task<Hash> HashingCopy(this Stream inputStream, Func<Memory<byte>, Task> fn,
        CancellationToken token)
    {
        using var rented = MemoryPool<byte>.Shared.Rent(1024 * 1024);
        var buffer = rented.Memory;

        var hasher = new xxHashAlgorithm(0);

        var running = true;
        ulong finalHash = 0;
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
            if (running)
            {
                hasher.TransformByteGroupsInternal(buffer.Span);
                await pendingWrite;
            }
            else
            {
                var preSize = (totalRead >> 5) << 5;
                if (preSize > 0)
                {
                    hasher.TransformByteGroupsInternal(buffer[..preSize].Span);
                    finalHash = hasher.FinalizeHashValueInternal(buffer[preSize..totalRead].Span);
                    await pendingWrite;
                    break;
                }

                finalHash = hasher.FinalizeHashValueInternal(buffer[..totalRead].Span);
                await pendingWrite;
                break;
            }
        }
        
        return new Hash(finalHash);
    }

    public static (Stream InputStream, Func<Hash> Fn) HashingPull(this Stream src)
    {
        var stream = new PullingStream(src);
        return (new BufferedStream(stream), () => stream.Hash);
    }

    class PullingStream : Stream
    {
        private readonly Stream _src;
        private readonly xxHashAlgorithm _hasher;
        private ulong? _hash;

        public PullingStream(Stream src)
        {
            _src = src;
            _hasher = new xxHashAlgorithm(0);
        }
        
        public Hash Hash => new(_hash ?? throw new InvalidOperationException("Hash not yet computed"));
        
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
            if (_hash.HasValue)
                throw new InvalidDataException("HashingPull can only be read once");
            
            var sized = count >> 5 << 5;
            if (sized == 0)
                throw new ArgumentException("count must be a multiple of 32, got " + count, nameof(count));
            
            
            var read = _src.ReadAtLeast(buffer, sized);
            
            if (read == 0)
                return 0;


            if (read == sized)
            {
                _hasher.TransformByteGroupsInternal(buffer.AsSpan(offset, read));
            }
            else
            {
                _hash = _hasher.FinalizeHashValueInternal(buffer.AsSpan(offset, read));
                return read;
            }

            return read;
        }
    }
}