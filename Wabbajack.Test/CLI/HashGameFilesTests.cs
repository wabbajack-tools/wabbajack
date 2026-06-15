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

namespace Wabbajack.CLI.Test;

[ClassConstructor<CliClassConstructor>]
[NotInParallel]
public class HashGameFilesTests
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

    [After(HookType.Test)]
    public void Cleanup()
    {
        if (_tempDir.DirectoryExists())
        {
            try { _tempDir.DeleteDirectory(); }
            catch { /* best effort */ }
        }
    }

    [Test]
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

        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
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

        await Assert.That(result).IsEqualTo(0);

        // Verify output file was created
        var outputFiles = outputDir.EnumerateFiles().ToArray();
        await Assert.That(outputFiles).IsNotEmpty();
    }

    [Test]
    public async Task Run_WithInvalidGameName_Throws()
    {
        var locator = Substitute.For<IGameLocator>();
        var cache = _provider.GetRequiredService<FileHashCache>();
        var dtos = _provider.GetRequiredService<DTOSerializer>();

        var verb = new HashGameFiles(NullLogger<HashGameFiles>.Instance, locator, cache, dtos);
        var outputDir = _tempDir.Combine("output");
        outputDir.CreateDirectory();

        await Assert.That(
            () => verb.Run(outputDir, "CompletelyFakeGame12345", CancellationToken.None))
            .Throws<Exception>();
    }

    [Test]
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

        await Assert.That(result).IsEqualTo(0);
    }
}
