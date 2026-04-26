using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Http;

public static class HttpClientExtensions
{
    public static IEnumerable<(string Key, string Value)> GetSetCookies(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("set-cookie", out var values))
            return Array.Empty<(string, string)>();

        return values
            .SelectMany(h => h.Split(";"))
            .Select(h => h.Split("="))
            .Where(h => h.Length == 2)
            .Select(h => (h[0], h[1]));
    }

    public static async Task<IMemoryOwner<byte>> ReadAsByteArrayAsync(this HttpContent content, IJob job,
        CancellationToken token)
    {
        await using var stream = await content.ReadAsStreamAsync(token);
        var memory = MemoryPool<byte>.Shared.Rent((int) job.Size!);

        while (job.Current < job.Size)
        {
            var read = await stream.ReadAsync(memory.Memory[(int) job.Current..(int) job.Size], token);
            await job.Report(read, token);
        }

        if (job.Current != job.Size)
            throw new Exception("Overread error");

        return memory;
    }
}