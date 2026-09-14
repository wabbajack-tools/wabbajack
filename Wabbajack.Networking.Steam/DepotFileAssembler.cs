using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SteamKit2;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Turns a manifest entry and a way of fetching chunks into a verified file on disk.
///     Split out from <see cref="SteamContentClient" /> because this is where a silent corruption would
///     live -- a chunk written at the wrong offset, a chunk that never arrived, a file accepted without
///     being checked -- and none of that is reachable for a test while it is tangled up with a live Steam
///     connection. <see cref="FetchChunk" /> is the only thing here that touches the network.
///     Three checks, cheapest first, because each catches something the others cannot:
///     <list type="bullet">
///         <item>
///             Every chunk must deliver exactly the bytes the manifest says it holds. A fetch that quietly
///             returned nothing would otherwise leave a hole of zeros in a preallocated file.
///         </item>
///         <item>
///             The assembled file must be exactly as long as the manifest says. Preallocation makes it that
///             length to begin with, so this catches a chunk that wrote past the end.
///         </item>
///         <item>
///             The whole file must hash to the SHA-1 Valve recorded for it. This is the one that catches a
///             chunk written at the wrong offset, and the reason none of the rest has to be trusted.
///         </item>
///     </list>
/// </summary>
public sealed class DepotFileAssembler
{
    /// <summary>
    ///     Fills <paramref name="destination" /> with one chunk's decrypted bytes and returns how many it
    ///     wrote. The buffer is rented and at least <see cref="DepotManifest.ChunkData.UncompressedLength" />
    ///     long.
    /// </summary>
    public delegate Task<int> FetchChunk(DepotManifest.ChunkData chunk, byte[] destination,
        CancellationToken token);

    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger _logger;

    /// <summary>Folders already swept this run. Sweeping is a start-up job, not a per-file one.</summary>
    private readonly HashSet<AbsolutePath> _swept = new();

    public DepotFileAssembler(ILogger logger, IResource<HttpClient> limiter)
    {
        _logger = logger;
        _limiter = limiter;
    }

    /// <summary>
    ///     Assembles <paramref name="file" /> at <paramref name="output" />.
    ///     Nothing lands at <paramref name="output" /> until every check has passed: the bytes go to a
    ///     randomly named <c>.wj_incoming</c> file beside it and are moved into place afterwards, so a
    ///     failed, cancelled or killed fetch cannot leave something that looks like a game file but is not.
    /// </summary>
    public async Task AssembleAsync(DepotManifest.FileData file, AbsolutePath output, FetchChunk fetch,
        CancellationToken token, IJob? parentJob = null)
    {
        if (file.FileHash is not {Length: > 0})
            throw new SteamContentVerificationException(
                $"The depot manifest carries no hash for '{file.FileName}', so nothing downloaded for it " +
                "could be checked. Refusing to write a file that cannot be verified.");

        output.Parent.CreateDirectory();
        SweepIncoming(output.Parent);

        // A random name rather than one derived from the output. Two fetches aimed at the same output would
        // otherwise share a temp file, and the first to fail would delete the one the second was still
        // writing into.
        var incoming = output.Parent.Combine(RandomName.Next()).WithExtension(Ext.WjIncoming);

        if (parentJob != null) parentJob.Size = (long) file.TotalSize;

        var chunks = file.Chunks ?? new List<DepotManifest.ChunkData>();

        try
        {
            using (var handle = File.OpenHandle(incoming.ToString(), FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, FileOptions.Asynchronous, (long) file.TotalSize))
            {
                // Chunks land at their own offsets, so the writes do not overlap and the order they finish
                // in does not matter. Preallocating means no chunk ever has to grow the file.
                RandomAccess.SetLength(handle, (long) file.TotalSize);

                await chunks.OrderBy(c => c.Offset).PDoAll(async chunk =>
                {
                    // The job comes first and the buffer second. A chunk is about a megabyte, and a large
                    // file has thousands of them; renting before waiting on the limiter would have every
                    // chunk in the file holding a buffer while only a handful were downloading.
                    using var job = await _limiter.Begin($"Downloading a chunk of {file.FileName}",
                        chunk.CompressedLength, token).ConfigureAwait(false);

                    var buffer = ArrayPool<byte>.Shared.Rent((int) chunk.UncompressedLength);
                    try
                    {
                        var written = await fetch(chunk, buffer, token).ConfigureAwait(false);

                        if (written != chunk.UncompressedLength)
                            throw new SteamContentVerificationException(
                                $"A chunk of '{file.FileName}' at offset {chunk.Offset} came back as {written} " +
                                $"bytes where the manifest says {chunk.UncompressedLength}");

                        await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, written), (long) chunk.Offset,
                            token).ConfigureAwait(false);

                        await job.Report(written, token).ConfigureAwait(false);

                        // Job.Report is not atomic, and every chunk is calling this one. The per-chunk jobs
                        // above are each touched by a single task, so only the parent's running total can
                        // drift, and it is a progress bar rather than a decision.
                        if (parentJob != null) await parentJob.Report(written, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }).ConfigureAwait(false);
            }

            var length = incoming.Size();
            if (length != (long) file.TotalSize)
                throw new SteamContentVerificationException(
                    $"'{file.FileName}' assembled to {length} bytes where the manifest says {file.TotalSize}");

            await VerifyAsync(incoming, file, token).ConfigureAwait(false);
            await incoming.MoveToAsync(output, true, token).ConfigureAwait(false);
        }
        catch
        {
            // Best effort, and deliberately not AbsolutePath.Delete: that retries a locked file five times
            // with a five second sleep between, so a file an antivirus happens to be reading would stall the
            // unwind for half a minute and then rethrow, replacing the real failure -- a cancellation, say --
            // with an IOException about cleanup.
            TryDelete(incoming);
            throw;
        }
    }

    /// <summary>
    ///     Checks the assembled file against the hash the manifest carries for it -- a SHA-1 of the whole
    ///     file, written by Valve at build time. SteamKit checks each chunk's own checksum as it decrypts,
    ///     so this is really a check that the chunks went to the right offsets, which no per-chunk check
    ///     can see.
    /// </summary>
    private static async Task VerifyAsync(AbsolutePath file, DepotManifest.FileData expected,
        CancellationToken token)
    {
        await using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var actual = await SHA1.HashDataAsync(stream, token).ConfigureAwait(false);

        if (actual.AsSpan().SequenceEqual(expected.FileHash)) return;

        throw new SteamContentVerificationException(
            $"'{expected.FileName}' does not match the hash the depot manifest carries for it " +
            $"(expected {Convert.ToHexString(expected.FileHash!)}, got {Convert.ToHexString(actual)})");
    }

    /// <summary>
    ///     Removes incoming files a previous run was killed in the middle of. A live one belonging to a
    ///     fetch running right now is held open with <see cref="FileShare.None" />, so deleting it fails and
    ///     is ignored, which is the whole reason this swallows.
    /// </summary>
    private void SweepIncoming(AbsolutePath folder)
    {
        lock (_swept)
        {
            if (!_swept.Add(folder)) return;
        }

        if (!folder.DirectoryExists()) return;

        foreach (var stale in folder.EnumerateFiles(Ext.WjIncoming, false))
        {
            if (TryDelete(stale))
                _logger.LogInformation("Removed {Stale}, left over from an interrupted fetch", stale);
        }
    }

    private static bool TryDelete(AbsolutePath file)
    {
        try
        {
            File.Delete(file.ToString());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
