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
        CancellationToken token, IJob? job = null)
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
}