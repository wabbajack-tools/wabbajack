using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SteamKit2;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Assembly is where a silent corruption would live: a chunk written at the wrong offset, a chunk that
///     never arrived, a file accepted without being checked. A real download proves the happy path and
///     nothing else, so these are the unhappy ones -- and every single failure has to end with no file at
///     the output path, because a wrong file under the right name is worse than no file at all.
/// </summary>
public class DepotFileAssemblerTests : IDisposable
{
    private const int ChunkSize = 4096;

    private readonly AbsolutePath _folder;
    private readonly Resource<HttpClient> _limiter = new("Test", 4);

    public DepotFileAssemblerTests()
    {
        _folder = KnownFolders.EntryPoint.Combine("assembler-test-" + RandomName.Next());
        _folder.CreateDirectory();
    }

    public void Dispose()
    {
        _folder.DeleteDirectory();
    }

    [Fact]
    public async Task AWholeFileIsAssembledFromItsChunksAndVerified()
    {
        var content = Content(ChunkSize * 3 + 17);
        var output = _folder.Combine("good.bin");

        await Assemble(content, output, Serve(content));

        Assert.Equal(content, await output.ReadAllBytesAsync());
    }

    [Fact]
    public async Task ChunksArriveInAnyOrderAndStillLandAtTheirOwnOffsets()
    {
        // The chunks are fetched concurrently and complete in whatever order the network hands them back.
        // Delaying the first one until the last has been served would reorder the writes if anything here
        // depended on arrival order.
        var content = Content(ChunkSize * 4);
        var output = _folder.Combine("reordered.bin");
        var lastServed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await Assemble(content, output, async (chunk, buffer, token) =>
        {
            if (chunk.Offset == 0)
                await lastServed.Task.WaitAsync(TimeSpan.FromSeconds(30), token);

            var written = Serve(content)(chunk, buffer, token).Result;

            if (chunk.Offset == (ulong) (ChunkSize * 3)) lastServed.SetResult();
            return written;
        });

        Assert.Equal(content, await output.ReadAllBytesAsync());
    }

    [Fact]
    public async Task AChunkThatNeverArrivesIsCaughtRatherThanLeavingAHoleOfZeros()
    {
        // The file is preallocated, so a chunk that returns nothing leaves zeros that are indistinguishable
        // from real content by length alone.
        var content = Content(ChunkSize * 3);
        var output = _folder.Combine("hole.bin");

        var ex = await Assert.ThrowsAsync<SteamContentVerificationException>(() =>
            Assemble(content, output, (chunk, buffer, token) =>
                chunk.Offset == (ulong) ChunkSize
                    ? Task.FromResult(0)
                    : Serve(content)(chunk, buffer, token)));

        Assert.Contains("came back as 0 bytes", ex.Message);
        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task AChunkThatOverrunsIsCaughtRatherThanGrowingTheFile()
    {
        var content = Content(ChunkSize * 2);
        var output = _folder.Combine("overrun.bin");

        var ex = await Assert.ThrowsAsync<SteamContentVerificationException>(() =>
            Assemble(content, output, async (chunk, buffer, token) =>
            {
                var written = await Serve(content)(chunk, buffer, token);
                return chunk.Offset == 0 ? written + 8 : written;
            }));

        Assert.Contains("where the manifest says", ex.Message);
        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task ChunksWrittenAtTheWrongOffsetAreCaughtByTheWholeFileHash()
    {
        // Every chunk is the right length and every chunk arrives, so only the hash can see this. It is the
        // reason the hash check exists at all.
        var content = Content(ChunkSize * 3);
        var output = _folder.Combine("swapped.bin");

        var ex = await Assert.ThrowsAsync<SteamContentVerificationException>(() =>
            Assemble(content, output, (chunk, buffer, _) =>
            {
                // Every chunk is served the first chunk's bytes: right lengths, right count, wrong content
                // at every offset but one.
                Array.Copy(content, 0, buffer, 0, (int) chunk.UncompressedLength);
                return Task.FromResult((int) chunk.UncompressedLength);
            }));

        Assert.Contains("does not match the hash", ex.Message);
        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task AManifestWithNoHashIsRefusedRatherThanAcceptedUnchecked()
    {
        // The hash is the only independent check there is. Writing a file that cannot be verified would be
        // the one door left open in the whole chain.
        var content = Content(ChunkSize);
        var output = _folder.Combine("unhashed.bin");

        var ex = await Assert.ThrowsAsync<SteamContentVerificationException>(() =>
            Assemble(content, output, Serve(content), hash: Array.Empty<byte>()));

        Assert.Contains("carries no hash", ex.Message);
        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task ACancelledFetchLeavesNothingBehind()
    {
        var content = Content(ChunkSize * 4);
        var output = _folder.Combine("cancelled.bin");
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Assemble(content, output, (chunk, buffer, token) =>
            {
                cancel.Cancel();
                token.ThrowIfCancellationRequested();
                return Serve(content)(chunk, buffer, token);
            }, token: cancel.Token));

        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task AFailedFetchReportsWhatFailedRatherThanWhatCleanupDid()
    {
        var content = Content(ChunkSize * 2);
        var output = _folder.Combine("failed.bin");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Assemble(content, output, (_, _, _) => throw new InvalidOperationException("the server went away")));

        Assert.Equal("the server went away", ex.Message);
        Assert.False(output.FileExists());
        AssertNothingLeftBehind();
    }

    [Fact]
    public async Task AnExistingOutputSurvivesAFailedFetch()
    {
        // The previous copy is the one the user still has. Overwriting it with a failure would be a
        // downgrade from "nothing happened" to "the file you had is gone".
        var output = _folder.Combine("existing.bin");
        await output.WriteAllBytesAsync(Encoding.UTF8.GetBytes("the copy that was already here").AsMemory());

        var content = Content(ChunkSize);
        await Assert.ThrowsAsync<SteamContentVerificationException>(() =>
            Assemble(content, output, (_, _, _) => Task.FromResult(0)));

        Assert.Equal("the copy that was already here", await output.ReadAllTextAsync());
    }

    [Fact]
    public async Task AnEmptyFileIsAssembledAndVerifiedLikeAnyOther()
    {
        var output = _folder.Combine("empty.bin");

        await Assemble(Array.Empty<byte>(), output, Serve(Array.Empty<byte>()));

        Assert.True(output.FileExists());
        Assert.Equal(0, output.Size());
    }

    [Fact]
    public async Task AnIncomingFileLeftByAKilledRunIsSweptUp()
    {
        var stale = _folder.Combine(RandomName.Next()).WithExtension(Ext.WjIncoming);
        await stale.WriteAllBytesAsync(Encoding.UTF8.GetBytes("half a download").AsMemory());

        var content = Content(ChunkSize);
        await Assemble(content, _folder.Combine("swept.bin"), Serve(content));

        Assert.False(stale.FileExists());
    }

    /// <summary>The bytes a whole file is made of, deterministic so a wrong offset shows up as a wrong hash.</summary>
    private static byte[] Content(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++) bytes[i] = (byte) (i * 31 % 251);
        return bytes;
    }

    /// <summary>A fetch that serves the real bytes, which is what a working CDN does.</summary>
    private static DepotFileAssembler.FetchChunk Serve(byte[] content)
    {
        return (chunk, buffer, _) =>
        {
            Array.Copy(content, (long) chunk.Offset, buffer, 0, (int) chunk.UncompressedLength);
            return Task.FromResult((int) chunk.UncompressedLength);
        };
    }

    private Task Assemble(byte[] content, AbsolutePath output, DepotFileAssembler.FetchChunk fetch,
        byte[]? hash = null, CancellationToken token = default)
    {
        var assembler = new DepotFileAssembler(NullLogger.Instance, _limiter);
        return assembler.AssembleAsync(FileData(content, hash), output, fetch, token);
    }

    private static DepotManifest.FileData FileData(byte[] content, byte[]? hash)
    {
        var file = new DepotManifest.FileData("Data\\Test.bin", Array.Empty<byte>(), 0, (ulong) content.Length,
            hash ?? SHA1.HashData(content), string.Empty, false, 0);

        for (var offset = 0; offset < content.Length; offset += ChunkSize)
        {
            var length = Math.Min(ChunkSize, content.Length - offset);
            file.Chunks.Add(new DepotManifest.ChunkData(Array.Empty<byte>(), 0, (ulong) offset, (uint) length,
                (uint) length));
        }

        return file;
    }

    /// <summary>No temp file, under any name, may outlive a failure.</summary>
    private void AssertNothingLeftBehind()
    {
        Assert.Empty(_folder.EnumerateFiles(Ext.WjIncoming, false));
    }
}
