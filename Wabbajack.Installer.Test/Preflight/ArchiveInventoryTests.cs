#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class ArchiveInventoryTests : IDisposable
{
    private readonly PreflightTestHost _host;

    public ArchiveInventoryTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private AbsolutePath Downloads => _host.Config.Downloads;

    private Task<Dictionary<Hash, AbsolutePath>> Scan(Archive[] archives,
        params AbsolutePath[] gameFolders)
    {
        return ArchiveInventory.Scan(archives, Downloads, gameFolders, _host.Cache, _host.Limiter,
            NullLogger.Instance, CancellationToken.None);
    }

    [Fact]
    public async Task OnlySizeMatchesAreHashed()
    {
        var wanted = await PreflightTestHost.WriteArchive(Downloads, "wanted.7z", "twelve bytes");
        var other = await PreflightTestHost.WriteFile(Downloads.Combine("other.7z"), "a different length entirely");

        var found = await Scan(new[] {wanted});

        Assert.Equal(Downloads.Combine("wanted.7z"), found[wanted.Hash]);
        Assert.NotEqual(default, await _host.Cache.TryGetHashCache(Downloads.Combine("wanted.7z")));
        Assert.Equal(default, await _host.Cache.TryGetHashCache(other));
    }

    [Fact]
    public async Task ASizeMatchWithTheWrongHashIsNotLinked()
    {
        var wanted = await PreflightTestHost.ArchiveFor("wanted.7z", "twelve bytes");
        await PreflightTestHost.WriteFile(Downloads.Combine("wanted.7z"), "twelve bytez");

        var found = await Scan(new[] {wanted});

        Assert.False(found.ContainsKey(wanted.Hash));
        Assert.Single(found);
    }

    [Fact]
    public async Task NoHashingHappensWithoutASizeMatch()
    {
        var wanted = await PreflightTestHost.ArchiveFor("wanted.7z", "twelve bytes");
        var file = await PreflightTestHost.WriteFile(Downloads.Combine("wanted.7z"), "not the same size");

        var found = await Scan(new[] {wanted});

        Assert.Empty(found);
        Assert.Equal(default, await _host.Cache.TryGetHashCache(file));
    }

    [Fact]
    public async Task TheNewestOfSeveralCopiesWins()
    {
        var archive = await PreflightTestHost.WriteArchive(Downloads, "old.7z", "same bytes");
        await PreflightTestHost.WriteFile(Downloads.Combine("new.7z"), "same bytes");
        // Creation before last-write, or the hash cache "repairs" the timestamps by touching the file.
        var old = new FileInfo(Downloads.Combine("old.7z").ToString());
        old.CreationTime = DateTime.Now.AddHours(-3);
        old.LastWriteTime = DateTime.Now.AddHours(-2);

        var found = await Scan(new[] {archive});

        Assert.Equal(Downloads.Combine("new.7z"), found[archive.Hash]);
    }

    [Fact]
    public async Task ScansTheGameFolderAndOtherGameFolders()
    {
        var other = _host.Manager.CreateFolder().Path;
        var inGame = await PreflightTestHost.WriteArchive(_host.GameFolder, "Data/Skyrim.esm", "game bytes");
        var inOther = await PreflightTestHost.WriteArchive(other, "Data/Fallout4.esm", "other bytes!");
        var missing = await PreflightTestHost.ArchiveFor("missing.7z", "nowhere");

        var found = await Scan(new[] {inGame, inOther, missing}, _host.GameFolder, other);

        Assert.Equal(_host.GameFolder.Combine("Data/Skyrim.esm"), found[inGame.Hash]);
        Assert.Equal(other.Combine("Data/Fallout4.esm"), found[inOther.Hash]);
        Assert.False(found.ContainsKey(missing.Hash));
    }

    [Fact]
    public async Task AGameFolderThatDoesNotExistIsIgnored()
    {
        var archive = await PreflightTestHost.WriteArchive(Downloads, "a.7z", "bytes");
        var found = await Scan(new[] {archive}, _host.Manager.CreateFolder().Path.Combine("gone"));
        Assert.Single(found);
    }

    [Fact]
    public void GameFoldersPreferTheConfiguredFolderAndSkipUnlocatableOtherGames()
    {
        var configured = _host.Manager.CreateFolder().Path;
        _host.Config.GameFolder = configured;
        _host.Config.OtherGames = new[] {Game.Fallout4, Game.Oblivion};
        var fallout = _host.Manager.CreateFolder().Path;
        _host.Locator.Games[Game.Fallout4] = fallout;

        var folders = ArchiveInventory.GameFolders(_host.Config, _host.Locator, NullLogger.Instance);

        Assert.Equal(new[] {configured, fallout}.OrderBy(p => p.ToString()), folders.OrderBy(p => p.ToString()));

        _host.Config.GameFolder = default;
        folders = ArchiveInventory.GameFolders(_host.Config, _host.Locator, NullLogger.Instance);
        Assert.Contains(_host.GameFolder, folders);
        Assert.DoesNotContain(configured, folders);
    }

    [Fact]
    public void InstallerPathThrowsWhenAnOtherGameIsNotInstalled()
    {
        _host.Config.OtherGames = new[] {Game.Fallout4, Game.Oblivion};
        _host.Locator.Games[Game.Fallout4] = _host.Manager.CreateFolder().Path;

        var ex = Assert.Throws<Exception>(() =>
            ArchiveInventory.GameFolders(_host.Config, _host.Locator, NullLogger.Instance, throwOnMissingOtherGame: true));

        Assert.Equal("Can't find game Oblivion", ex.Message);
    }

    [Fact]
    public void PreflightPathSkipsAnOtherGameThatIsNotInstalled()
    {
        _host.Config.OtherGames = new[] {Game.Fallout4, Game.Oblivion};
        var fallout = _host.Manager.CreateFolder().Path;
        _host.Locator.Games[Game.Fallout4] = fallout;

        var folders = ArchiveInventory.GameFolders(_host.Config, _host.Locator, NullLogger.Instance);

        Assert.Equal(new[] {_host.GameFolder, fallout}.OrderBy(p => p.ToString()), folders.OrderBy(p => p.ToString()));
    }

    [Fact]
    public async Task TheCheckMarksPresentAndMissingAndLeavesGameFilesToGameFiles()
    {
        // Lives in the game folder so the downloads folder can be absent, to prove the check creates it.
        var present = await PreflightTestHost.WriteArchive(_host.GameFolder, "present.7z", "present bytes");
        var missing = await PreflightTestHost.ArchiveFor("missing.7z", "missing bytes!");
        var gameFile = await PreflightTestHost.ArchiveFor("Skyrim.esm", "game", new GameFileSource
        {
            Game = Game.SkyrimSpecialEdition, GameFile = "Data/Skyrim.esm".ToRelativePath()
        });
        _host.Config.ModList.Archives = new[] {present, missing, gameFile};
        _host.Config.ModList.Directives = new[] {present, missing, gameFile}.Select((a, i) => (Directive) new FromArchive
        {
            To = $"mods/{i}.txt".ToRelativePath(), Hash = a.Hash, Size = a.Size,
            ArchiveHashPath = new HashRelativePath(a.Hash, "file.txt".ToRelativePath())
        }).ToArray();
        Downloads.DeleteDirectory();

        var ctx = _host.Context();
        ctx.State.GameFolder = _host.GameFolder;
        var progress = new RecordingProgress();
        var result = await new ArchiveInventoryCheck().Run(ctx, progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        // The absent game file is not "to download": that is game-files' finding, not a download.
        Assert.Contains("1 of 3 archives present, 1 to download", result.Message);
        Assert.True(Downloads.DirectoryExists());
        Assert.Equal(3, ctx.State.RequiredArchives.Length);
        Assert.Equal(_host.GameFolder.Combine("present.7z"), ctx.State.HashedArchives["PRESENT.7z"]);
        Assert.Equal(new[] {"missing.7z"}, ctx.State.Missing.Select(a => a.Name));
        Assert.Equal(missing.Size, ctx.State.RemainingDownloadBytes);

        var states = progress.LastStates();
        Assert.Equal(ArchiveState.Present, states["present.7z"]);
        Assert.Equal(ArchiveState.Missing, states["missing.7z"]);
        Assert.Equal(ArchiveState.Missing, states["Skyrim.esm"]);
    }

    [Fact]
    public async Task TheCheckDoesNotDemandArchivesAnExistingInstallNoLongerNeeds()
    {
        var installed = await PreflightTestHost.ArchiveFor("installed.7z", "installed bytes");
        var needed = await PreflightTestHost.ArchiveFor("needed.7z", "needed bytes");
        _host.Config.ModList.Archives = new[] {installed, needed};
        _host.Config.ModList.Directives = new Directive[]
        {
            new FromArchive
            {
                To = "mods/a/file.txt".ToRelativePath(), Hash = await PreflightTestHost.HashOf("file a"), Size = 6,
                ArchiveHashPath = new HashRelativePath(installed.Hash, "file.txt".ToRelativePath())
            },
            new FromArchive
            {
                To = "mods/b/file.txt".ToRelativePath(), Hash = await PreflightTestHost.HashOf("file b"), Size = 6,
                ArchiveHashPath = new HashRelativePath(needed.Hash, "file.txt".ToRelativePath())
            }
        };
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods/a/file.txt"), "file a");

        var ctx = _host.Context();
        ctx.State.GameFolder = _host.GameFolder;
        var result = await new ArchiveInventoryCheck().Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(new[] {"needed.7z"}, ctx.State.RequiredArchives.Select(a => a.Name));
        Assert.Equal(new[] {"needed.7z"}, ctx.State.Missing.Select(a => a.Name));
    }

    /// <summary>
    ///     Candidates are found by size, so every archive of a given size claims every file of that size. A
    ///     file claimed by several archives used to be queued once per archive and hashed once per archive,
    ///     which on a list with a few common sizes is a lot of a large downloads folder read twice over.
    /// </summary>
    [Fact]
    public async Task AFileMatchingSeveralArchivesBySizeIsHashedOnce()
    {
        var wanted = await PreflightTestHost.WriteArchive(Downloads, "wanted.7z", "twelve bytes");

        // Same size, different bytes: both archives claim the one file on disk as a candidate.
        var sameSize = await PreflightTestHost.ArchiveFor("other.7z", "TWELVE BYTES");
        Assert.Equal(wanted.Size, sameSize.Size);
        Assert.NotEqual(wanted.Hash, sameSize.Hash);

        var announced = 0;
        var hashed = 0;
        var found = await ArchiveInventory.Scan(new[] {wanted, sameSize}, Downloads,
            Array.Empty<AbsolutePath>(), _host.Cache, _host.Limiter, NullLogger.Instance, CancellationToken.None,
            count => announced = count, () => Interlocked.Increment(ref hashed));

        Assert.Equal(Downloads.Combine("wanted.7z"), found[wanted.Hash]);
        Assert.False(found.ContainsKey(sameSize.Hash));
        Assert.Equal(1, announced);
        Assert.Equal(1, hashed);
    }
}
