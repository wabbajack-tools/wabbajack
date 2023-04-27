using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Http;

public class SingleThreadedDownloader : IHttpDownloader
{
    private readonly HttpClient _client;
    private readonly ILogger<SingleThreadedDownloader> _logger;

    public SingleThreadedDownloader(ILogger<SingleThreadedDownloader> logger, HttpClient client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<Hash> Download(HttpRequestMessage message, AbsolutePath outputPath, IJob job,
        CancellationToken token)
    {
        var downloader = new ResumableDownloader(message, outputPath, job);
        return await downloader.Download(token);

        // using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        // if (!response.IsSuccessStatusCode)
        //     throw new HttpException(response);
        //
        // if (job.Size == 0)
        //     job.Size = response.Content.Headers.ContentLength ?? 0;
        //
        // /* Need to make this mulitthreaded to be much use
        // if ((response.Content.Headers.ContentLength ?? 0) != 0 &&
        //     response.Headers.AcceptRanges.FirstOrDefault() == "bytes")
        // {
        //     return await ResettingDownloader(response, message, outputPath, job, token);
        // }
        // */
        //
        // await using var stream = await response.Content.ReadAsStreamAsync(token);
        // await using var outputStream = outputPath.Open(FileMode.Create, FileAccess.Write);
        // return await stream.HashingCopy(outputStream, token, job);
    }

    private const int CHUNK_SIZE = 1024 * 1024 * 8;

    private async Task<Hash> ResettingDownloader(HttpResponseMessage response, HttpRequestMessage message, AbsolutePath outputPath, IJob job, CancellationToken token)
    {

        using var rented = MemoryPool<byte>.Shared.Rent(CHUNK_SIZE);
        var buffer = rented.Memory;

        var hasher = new xxHashAlgorithm(0);

        var running = true;
        ulong finalHash = 0;

        var inputStream = await response.Content.ReadAsStreamAsync(token);
        await using var outputStream = outputPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        long writePosition = 0;

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

            {
                writePosition += totalRead;
                if (job != null)
                    await job.Report(totalRead, token);
                message = CloneMessage(message);
                message.Headers.Range = new RangeHeaderValue(writePosition, writePosition + CHUNK_SIZE);
                await inputStream.DisposeAsync();
                response.Dispose();
                response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
                HttpException.ThrowOnFailure(response);
                inputStream = await response.Content.ReadAsStreamAsync(token);
            }
        }

        await outputStream.FlushAsync(token);

        return new Hash(finalHash);
    }

    private HttpRequestMessage CloneMessage(HttpRequestMessage message)
    {
        var newMsg = new HttpRequestMessage(message.Method, message.RequestUri);
        foreach (var header in message.Headers)
        {
            newMsg.Headers.Add(header.Key, header.Value);
        }
        return newMsg;
    }
}