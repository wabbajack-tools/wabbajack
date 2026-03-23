using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wabbajack.CLI.Verbs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;
using Xunit;

namespace Wabbajack.CLI.Test;

[Collection("CLI")]
public class HashGameFilesTests : IDisposable
{
    private readonly AbsolutePath _tempDir;
    private readonly IServiceProvider _provider;

    public HashGameFilesTests(CLITestFixture fixture)
    {
        _provider = fixture.ServiceProvider;
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "wj-hgf-test-" + Guid.NewGuid().ToString("N")[..8])
            .ToAbsolutePath();
        _tempDir.CreateDirectory();
    }

    public void Dispose()
    {
        if (_tempDir.DirectoryExists())
        {
            try { _tempDir.DeleteDirectory(); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Run_GameNotInstalled_ReturnsOne()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.GameLocation(Arg.Any<Game>()).Returns(_ => throw new Exception("Game not installed"));

        var cache = _provider.GetRequiredService<FileHashCache>();
        var dtos = _provider.GetRequiredService<DTOSerializer>();

        var verb = new HashGameFiles(NullLogger<HashGameFiles>.Instance, locator, cache, dtos);
        var outputDir = _tempDir.Combine("output");
        outputDir.CreateDirectory();

        var result = await verb.Run(outputDir, "SkyrimSpecialEdition", CancellationToken.None);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Run_WithGameFiles_HashesAndWritesJson()
    {
        var gameDir = _tempDir.Combine("game");
        gameDir.CreateDirectory();

        // Create some fake game files
        await gameDir.Combine("data.bsa".ToRelativePath()).WriteAllBytesAsync(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        await gameDir.Combine("plugin.esp".ToRelativePath()).WriteAllBytesAsync(
            new byte[] { 10, 20, 30, 40, 50 });

        // Pick a game with a MainExecutable
        var testGame = Game.SkyrimSpecialEdition;
        var gameMeta = testGame.MetaData();

        // Create a dummy main executable
        if (gameMeta.MainExecutable != null)
        {
            var exePath = gameMeta.MainExecutable.Value.RelativeTo(gameDir);
            exePath.Parent.CreateDirectory();
            await exePath.WriteAllBytesAsync(new byte[] { 0x4D, 0x5A, 0, 0 });
        }

        var locator = Substitute.For<IGameLocator>();
        locator.GameLocation(testGame).Returns(gameDir);

        var cache = _provider.GetRequiredService<FileHashCache>();
        var dtos = _provider.GetRequiredService<DTOSerializer>();

        var verb = new HashGameFiles(NullLogger<HashGameFiles>.Instance, locator, cache, dtos);
        var outputDir = _tempDir.Combine("output");
        outputDir.CreateDirectory();

        var result = await verb.Run(outputDir, "SkyrimSpecialEdition", CancellationToken.None);

        Assert.Equal(0, result);

        // Verify output file was created
        var outputFiles = outputDir.EnumerateFiles().ToArray();
        Assert.NotEmpty(outputFiles);
    }

    [Fact]
    public async Task Run_WithInvalidGameName_Throws()
    {
        var locator = Substitute.For<IGameLocator>();
        var cache = _provider.GetRequiredService<FileHashCache>();
        var dtos = _provider.GetRequiredService<DTOSerializer>();

        var verb = new HashGameFiles(NullLogger<HashGameFiles>.Instance, locator, cache, dtos);
        var outputDir = _tempDir.Combine("output");
        outputDir.CreateDirectory();

        await Assert.ThrowsAnyAsync<Exception>(
            () => verb.Run(outputDir, "CompletelyFakeGame12345", CancellationToken.None));
    }

    [Fact]
    public async Task Run_WithEmptyGameFolder_WritesEmptyJson()
    {
        var gameDir = _tempDir.Combine("empty-game");
        gameDir.CreateDirectory();

        var testGame = Game.SkyrimSpecialEdition;
        var gameMeta = testGame.MetaData();

        // Create only the main executable (no other game files)
        if (gameMeta.MainExecutable != null)
        {
            var exePath = gameMeta.MainExecutable.Value.RelativeTo(gameDir);
            exePath.Parent.CreateDirectory();
            await exePath.WriteAllBytesAsync(new byte[] { 0x4D, 0x5A });
        }

        var locator = Substitute.For<IGameLocator>();
        locator.GameLocation(testGame).Returns(gameDir);

        var cache = _provider.GetRequiredService<FileHashCache>();
        var dtos = _provider.GetRequiredService<DTOSerializer>();

        var verb = new HashGameFiles(NullLogger<HashGameFiles>.Instance, locator, cache, dtos);
        var outputDir = _tempDir.Combine("output2");
        outputDir.CreateDirectory();

        var result = await verb.Run(outputDir, "SkyrimSpecialEdition", CancellationToken.None);

        Assert.Equal(0, result);
    }
}
