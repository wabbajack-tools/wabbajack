using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

public class DumpZipInfoTests : IDisposable
{
    private readonly AbsolutePath _zipFile;

    public DumpZipInfoTests()
    {
        _zipFile = Path.GetTempFileName().ToAbsolutePath();
    }

    public void Dispose()
    {
        if (_zipFile.FileExists()) _zipFile.Delete();
    }

    private void CreateTestZip(params (string name, byte[] content)[] entries)
    {
        using var stream = _zipFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var entryStream = entry.Open();
            entryStream.Write(content);
        }
    }

    [Fact]
    public async Task Run_ListMode_ReturnsZero()
    {
        CreateTestZip(
            ("file1.txt", Encoding.UTF8.GetBytes("hello")),
            ("subdir/file2.txt", Encoding.UTF8.GetBytes("world"))
        );

        var verb = new DumpZipInfo(NullLogger<DumpZipInfo>.Instance);
        var result = await verb.Run(_zipFile, test: false);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_TestMode_ExtractsAndReturnsZero()
    {
        CreateTestZip(
            ("file1.txt", Encoding.UTF8.GetBytes("hello")),
            ("file2.bin", new byte[] { 0xFF, 0xFE, 0xFD })
        );

        var verb = new DumpZipInfo(NullLogger<DumpZipInfo>.Instance);
        var result = await verb.Run(_zipFile, test: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_EmptyZip_ReturnsZero()
    {
        CreateTestZip();

        var verb = new DumpZipInfo(NullLogger<DumpZipInfo>.Instance);

        Assert.Equal(0, await verb.Run(_zipFile, test: false));
        Assert.Equal(0, await verb.Run(_zipFile, test: true));
    }

    [Fact]
    public async Task Run_ManyFiles_ReturnsZero()
    {
        var entries = new (string, byte[])[50];
        for (var i = 0; i < 50; i++)
            entries[i] = ($"file_{i:D3}.txt", Encoding.UTF8.GetBytes($"content {i}"));

        CreateTestZip(entries);

        var verb = new DumpZipInfo(NullLogger<DumpZipInfo>.Instance);
        var result = await verb.Run(_zipFile, test: false);

        Assert.Equal(0, result);
    }
}
