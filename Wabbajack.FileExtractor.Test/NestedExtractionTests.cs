using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Common;
using Wabbajack.FileExtractor.ExtractedFiles;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.FileExtractor.Test;

/// <summary>
///     The VFS extracts an archive by calling back into the extractor for each nested archive it finds, so a
///     job that is still held while that callback runs takes a slot the callback needs. With enough slots the
///     mistake is invisible; with one slot it stops dead.
/// </summary>
public class NestedExtractionTests
{
    private static readonly Extension ZipExtension = new(".zip");

    private readonly TemporaryFileManager _manager = new();

    private static void WriteZip(AbsolutePath path, params (string Name, byte[] Content)[] entries)
    {
        using var stream = path.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var entryStream = archive.CreateEntry(name).Open();
            entryStream.Write(content, 0, content.Length);
        }
    }

    /// <summary>
    ///     Three levels deep against a single-slot limiter. If any extraction path holds its job across the
    ///     callback, this never returns.
    /// </summary>
    [Fact]
    public async Task NestedExtraction_WithASingleSlotLimiter_DoesNotDeadlock()
    {
        await using var folder = _manager.CreateFolder();

        var innermost = folder.Path.Combine("innermost.zip");
        WriteZip(innermost, ("payload.txt", "cheese"u8.ToArray()));

        var middle = folder.Path.Combine("middle.zip");
        WriteZip(middle, ("innermost.zip", await innermost.ReadAllBytesAsync()));

        var outer = folder.Path.Combine("outer.zip");
        WriteZip(outer, ("middle.zip", await middle.ReadAllBytesAsync()));

        var extractor = new FileExtractor(NullLogger<FileExtractor>.Instance,
            new ParallelOptions {MaxDegreeOfParallelism = 1},
            _manager,
            new Resource<FileExtractor>("Single slot extractor", 1));

        var depth = 0;

        async ValueTask<int> Recurse(RelativePath path, IExtractedFile file)
        {
            if (path.Extension != ZipExtension) return 0;

            Interlocked.Increment(ref depth);
            var inner = await extractor.GatheringExtract(file, _ => true, Recurse, CancellationToken.None);
            return inner.Count;
        }

        var extraction = extractor.GatheringExtract(new NativeFileStreamFactory(outer), _ => true, Recurse,
            CancellationToken.None);

        var finished = await Task.WhenAny(extraction, Task.Delay(TimeSpan.FromSeconds(120)));
        Assert.True(finished == extraction,
            "nested extraction did not finish - a job is being held across the extraction callback");

        var results = await extraction;
        Assert.Single(results);
        Assert.Equal(2, depth);
    }

    /// <summary>
    ///     A single slot must actually be handed back between top-level archives too, so a sequence of
    ///     extractions cannot leak the limiter.
    /// </summary>
    [Fact]
    public async Task RepeatedExtraction_WithASingleSlotLimiter_ReleasesItsJob()
    {
        await using var folder = _manager.CreateFolder();

        var src = folder.Path.Combine("simple.zip");
        WriteZip(src, ("payload.txt", "cheese"u8.ToArray()));

        var extractor = new FileExtractor(NullLogger<FileExtractor>.Instance,
            new ParallelOptions {MaxDegreeOfParallelism = 1},
            _manager,
            new Resource<FileExtractor>("Single slot extractor", 1));

        for (var i = 0; i < 5; i++)
        {
            var extraction = extractor.GatheringExtract(new NativeFileStreamFactory(src), _ => true,
                async (_, file) =>
                {
                    await using var s = await file.GetStream();
                    using var sr = new StreamReader(s, Encoding.UTF8);
                    return await sr.ReadToEndAsync();
                }, CancellationToken.None);

            var finished = await Task.WhenAny(extraction, Task.Delay(TimeSpan.FromSeconds(60)));
            Assert.True(finished == extraction, $"extraction {i} stalled - the limiter job was not released");

            var results = await extraction;
            Assert.Equal("cheese", Assert.Single(results).Value);
        }
    }
}
