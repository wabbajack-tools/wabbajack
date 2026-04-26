using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

public class HashFileTests : IDisposable
{
    private readonly AbsolutePath _tempFile;

    public HashFileTests()
    {
        _tempFile = Path.GetTempFileName().ToAbsolutePath();
    }

    public void Dispose()
    {
        if (_tempFile.FileExists()) _tempFile.Delete();
    }

    [Fact]
    public async Task Run_WithValidFile_ReturnsZero()
    {
        await _tempFile.WriteAllBytesAsync(new byte[] { 1, 2, 3, 4, 5 });

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithEmptyFile_ReturnsZero()
    {
        await _tempFile.WriteAllBytesAsync(Array.Empty<byte>());

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithLargeFile_ReturnsZero()
    {
        var data = new byte[1024 * 1024];
        new Random(42).NextBytes(data);
        await _tempFile.WriteAllBytesAsync(data);

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        Assert.Equal(0, result);
    }
}
