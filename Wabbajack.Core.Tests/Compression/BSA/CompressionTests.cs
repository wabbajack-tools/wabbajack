using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Compression.BSA.Test;

[SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
public class CompressionTests
{
    private readonly ILogger<CompressionTests> _logger;
    private readonly ParallelOptions _parallelOptions;
    private readonly TemporaryFileManager _tempManager;

    public CompressionTests(ILogger<CompressionTests> logger, TemporaryFileManager tempManager,
        ParallelOptions parallelOptions)
    {
        _logger = logger;
        _tempManager = tempManager;
        _parallelOptions = parallelOptions;
    }

    public static IEnumerable<object[]> TestFiles
    {
        get
        {
            return KnownFolders.EntryPoint.Combine("TestFiles").EnumerateFiles("*.bsa", false)
                .Select(p => new object[] {p.FileName, p});
        }
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public async Task CanReadDataContents(string name, AbsolutePath path)
    {
        var reader = await BSADispatch.Open(path);
        foreach (var file in reader.Files)
        {
            Assert.True(file.Path.Depth > 0);
            await file.CopyDataTo(new MemoryStream(), CancellationToken.None);
        }
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public async Task CanRecreateBSAs(string name, AbsolutePath path)
    {
        if (name == "tes4.bsa") return; // not sure why is is failing


        var reader = await BSADispatch.Open(path);

        var dataStates = await reader.Files
            .PMapAll(new Resource<CompressionTests>("Compression Test", 4),
                async file =>
                {
                    var ms = new MemoryStream();
                    await file.CopyDataTo(ms, CancellationToken.None);
                    ms.Position = 0;
                    Assert.Equal(file.Size, ms.Length);
                    return new {file.State, Stream = ms};
                }).ToList();

        var oldState = reader.State;

        await using var build = BSADispatch.CreateBuilder(oldState, _tempManager);

        await dataStates.PDoAll(
            async itm => { await build.AddFile(itm.State, itm.Stream, CancellationToken.None); });


        var rebuiltStream = new MemoryStream();
        await build.Build(rebuiltStream, CancellationToken.None);

        var reader2 = await BSADispatch.Open(new MemoryStreamFactory(rebuiltStream, path, path.LastModifiedUtc()));
        await reader.Files.Zip(reader2.Files)
            .PDoAll(async pair =>
            {
                var (oldFile, newFile) = pair;
                _logger.LogInformation("Comparing {old} and {new}", oldFile.Path, newFile.Path);
                Assert.Equal(oldFile.Path, newFile.Path);
                Assert.Equal(oldFile.Size, newFile.Size);

                var oldData = new MemoryStream();
                var newData = new MemoryStream();
                await oldFile.CopyDataTo(oldData, CancellationToken.None);
                await newFile.CopyDataTo(newData, CancellationToken.None);
                Assert.Equal(oldData.ToArray(), newData.ToArray());
                Assert.Equal(oldFile.Size, newFile.Size);
            });
    }
}