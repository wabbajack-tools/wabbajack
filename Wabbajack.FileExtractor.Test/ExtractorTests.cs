using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.FileExtractor.Test;

public class ExtractorTests
{
    private readonly FileExtractor _extractor;
    private readonly IResource<FileExtractor> _limiter;
    private readonly TemporaryFileManager _manager;

    public ExtractorTests(FileExtractor extractor, TemporaryFileManager manager, IResource<FileExtractor> limiter)
    {
        _extractor = extractor;
        _manager = manager;
        _limiter = limiter;
    }

    [Fact]
    public async Task CanExtract7z()
    {
        var src = KnownFolders.EntryPoint.Combine("TestData", "cheese.7z");
        var results = await _extractor.GatheringExtractWith7Zip(
            new NativeFileStreamFactory(src), path => true,
            async (path, file) =>
            {
                await using var s = await file.GetStream();
                using var sr = new StreamReader(s);
                return new {Path = path, Data = await sr.ReadToEndAsync()};
            }, null, CancellationToken.None);

        Assert.True(results.Count == 1);
    }

    [Fact]
    public async Task GatheringExtractWith7Zip_WithCaseVariantDuplicates_DoesNotThrow()
    {
        await using var tmp = _manager.CreateFile(new Extension(".zip"));

        var zipBytes = new MemoryStream();
        using (var archive = new ZipArchive(zipBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s1 = archive.CreateEntry("subdir/file.txt").Open())
                s1.Write(Encoding.UTF8.GetBytes("hello"));
            using (var s2 = archive.CreateEntry("subdir/FILE.txt").Open())
                s2.Write(Encoding.UTF8.GetBytes("world"));
        }
        tmp.Path.WriteAllBytes(zipBytes.ToArray());

        var results = await _extractor.GatheringExtractWith7Zip(
            new NativeFileStreamFactory(tmp.Path), _ => true,
            async (path, file) =>
            {
                await using var s = await file.GetStream();
                using var sr = new StreamReader(s);
                return await sr.ReadToEndAsync();
            }, null, CancellationToken.None);

        Assert.Equal(1, results.Count);
    }

    [Fact]
    public async Task CanExtractWithGatheringExtract()
    {
        var src = KnownFolders.EntryPoint.Combine("TestData", "cheese.7z");
        var results = await _extractor.GatheringExtract(
            new NativeFileStreamFactory(src), path => true,
            async (path, file) =>
            {
                await using var s = await file.GetStream();
                using var sr = new StreamReader(s);
                return new {Path = path, Data = await sr.ReadToEndAsync()};
            }, CancellationToken.None);

        Assert.True(results.Count == 1);
    }
}