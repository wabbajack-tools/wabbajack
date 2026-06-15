using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Test;

public class HashFileTests
{
    private readonly AbsolutePath _tempFile;

    public HashFileTests()
    {
        _tempFile = Path.GetTempFileName().ToAbsolutePath();
    }

    [After(HookType.Test)]
    public void Cleanup()
    {
        if (_tempFile.FileExists()) _tempFile.Delete();
    }

    [Test]
    public async Task Run_WithValidFile_ReturnsZero()
    {
        await _tempFile.WriteAllBytesAsync(new byte[] { 1, 2, 3, 4, 5 });

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithEmptyFile_ReturnsZero()
    {
        await _tempFile.WriteAllBytesAsync(Array.Empty<byte>());

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithLargeFile_ReturnsZero()
    {
        var data = new byte[1024 * 1024];
        new Random(42).NextBytes(data);
        await _tempFile.WriteAllBytesAsync(data);

        var verb = new HashFile(NullLogger<HashFile>.Instance);
        var result = await verb.Run(_tempFile);

        await Assert.That(result).IsEqualTo(0);
    }
}
