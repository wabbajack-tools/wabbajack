using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
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