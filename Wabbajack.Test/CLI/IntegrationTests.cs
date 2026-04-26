using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Verbs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

[Collection("CLI")]
public class IntegrationTests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly AbsolutePath _tempDir;

    public IntegrationTests(CLITestFixture fixture)
    {
        _provider = fixture.ServiceProvider;
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "wj-cli-test-" + Guid.NewGuid().ToString("N")[..8])
            .ToAbsolutePath();
        _tempDir.CreateDirectory();
    }

    public void Dispose()
    {
        if (_tempDir.DirectoryExists())
        {
            try { _tempDir.DeleteDirectory(); }
            catch { /* best effort cleanup */ }
        }
    }

    private AbsolutePath CreateTestZip(string name, params (string entryName, string content)[] entries)
    {
        var zipPath = _tempDir.Combine(name);
        using var stream = zipPath.Open(System.IO.FileMode.Create, System.IO.FileAccess.Write,
            System.IO.FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new System.IO.StreamWriter(entry.Open());
            writer.Write(content);
        }

        return zipPath;
    }

    [Fact]
    public async Task Extract_WithValidZip_ExtractsFiles()
    {
        var zipPath = CreateTestZip("test.zip",
            ("hello.txt", "Hello, World!"),
            ("subdir/nested.txt", "Nested content"));
        var outputDir = _tempDir.Combine("output");
        outputDir.CreateDirectory();

        var verb = _provider.GetRequiredService<Extract>();
        var result = await verb.Run(zipPath, outputDir, CancellationToken.None);

        Assert.Equal(0, result);
        Assert.True(outputDir.Combine("hello.txt".ToRelativePath()).FileExists());
    }

    [Fact]
    public async Task Extract_WithEmptyZip_ReturnsZero()
    {
        var zipPath = CreateTestZip("empty.zip");
        var outputDir = _tempDir.Combine("output-empty");
        outputDir.CreateDirectory();

        var verb = _provider.GetRequiredService<Extract>();
        var result = await verb.Run(zipPath, outputDir, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Extract_WithNestedDirectories_ExtractsAll()
    {
        var zipPath = CreateTestZip("nested.zip",
            ("a/b/c.txt", "deep file"),
            ("root.txt", "root file"),
            ("a/sibling.txt", "sibling"));
        var outputDir = _tempDir.Combine("output-nested");
        outputDir.CreateDirectory();

        var verb = _provider.GetRequiredService<Extract>();
        var result = await verb.Run(zipPath, outputDir, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Compile_WithNonExistentPath_Throws()
    {
        var verb = _provider.GetRequiredService<Compile>();
        var nonExistent = _tempDir.Combine("does-not-exist");

        await Assert.ThrowsAnyAsync<Exception>(
            () => verb.Run(nonExistent, _tempDir, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadUrl_WithUnsupportedScheme_Throws()
    {
        var verb = _provider.GetRequiredService<DownloadUrl>();
        var output = _tempDir.Combine("output.bin");

        await Assert.ThrowsAnyAsync<Exception>(
            () => verb.Run(new Uri("file:///not/a/real/download"), output));
    }

    [Fact]
    public async Task HashGameFiles_WithInvalidGame_ThrowsException()
    {
        var verb = _provider.GetRequiredService<HashGameFiles>();

        await Assert.ThrowsAnyAsync<Exception>(
            () => verb.Run(_tempDir, "NotARealGame12345", CancellationToken.None));
    }
}
