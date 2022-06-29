using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.Common;

public static class StreamExtensions
{
    public static async Task CopyToLimitAsync(this Stream frm, Stream tw, int limit, CancellationToken token)
    {
        using var buff = MemoryPool<byte>.Shared.Rent(1024 * 128);
        var buffMemory = buff.Memory;
        while (limit > 0 && !token.IsCancellationRequested)
        {
            var toRead = Math.Min(buffMemory.Length, limit);
            var read = await frm.ReadAsync(buffMemory[..toRead], token);
            if (read == 0)
                throw new Exception("End of stream before end of limit");
            await tw.WriteAsync(buffMemory[..read], token);
            limit -= read;
        }

        await tw.FlushAsync(token);
    }

    public static async Task CopyToWithStatusAsync(this Stream input, long maxSize, Stream output,
        CancellationToken token)
    {
        var buffer = new byte[1024 * 1024];
        if (maxSize == 0) maxSize = 1;
        long totalRead = 0;
        var remain = maxSize;
        while (true)
        {
            var toRead = Math.Min(buffer.Length, remain);
            var read = await input.ReadAsync(buffer.AsMemory(0, (int) toRead), token);
            remain -= read;
            if (read == 0) break;
            totalRead += read;
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }

        await output.FlushAsync(token);
    }

    public static async Task<byte[]> ReadAllAsync(this Stream stream)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public static string ReadAllText(this Stream stream)
    {
        using var sr = new StreamReader(stream);
        return sr.ReadToEnd();
    }

    public static async Task<T> FromJson<T>(this Stream stream, DTOSerializer? dtos = null)
    {
        return (await JsonSerializer.DeserializeAsync<T>(stream, dtos?.Options))!;
    }

    public static async IAsyncEnumerable<string> ReadLinesAsync(this Stream stream)
    {
        using var sr = new StreamReader(stream);
        while (true)
        {
            var data = await sr.ReadLineAsync();
            if (data == null) break;
            yield return data!;
        }
    }
}