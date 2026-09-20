using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

/// <summary>
///     The content index on disk. What matters here is that it merges: the index is a repository several
///     people add to one build at a time over years, so a run that rewrote a shard from what it fetched
///     today would delete every other build's files from it. The rest is resumability - a run stopped half
///     way through fifteen gigabytes must not start again from the beginning.
/// </summary>
[Collection("CLI")]
public class GameFileIndexFolderTests : IDisposable
{
    private const uint App = 489830;
    private readonly DTOSerializer _dtos;
    private readonly AbsolutePath _root;

    public GameFileIndexFolderTests(CLITestFixture fixture)
    {
        _dtos = fixture.ServiceProvider.GetRequiredService<DTOSerializer>();
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "wj-index-test-" + Guid.NewGuid().ToString("N")[..8]).ToAbsolutePath();
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        if (!_root.DirectoryExists()) return;
        try
        {
            _root.DeleteDirectory();
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public async Task AGameNobodyHasIndexedIsAnEmptyIndex()
    {
        var index = await Load();

        Assert.Equal(0, index.FileCount);
        Assert.Empty(index.Manifests);
        Assert.False(index.HasManifest(App, 1, 100));
    }

    /// <summary>
    ///     A hash lands in the shard named after its first byte, because that is the file a lookup fetches -
    ///     it is the whole reason the index is split up at all.
    /// </summary>
    [Fact]
    public async Task FilesAreWrittenToTheShardTheirHashNames()
    {
        var index = await Load();
        var hash = Hash.FromHex("ab00000000000000");

        index.Add(File(hash, "Data\\Skyrim.esm", 1, 100));
        await index.Save(CancellationToken.None);

        Assert.Equal("ab", GameFileIndex.ShardOf(hash));
        Assert.True(Shard("ab").FileExists());

        var read = await Load();
        Assert.Equal(1, read.FileCount);
        Assert.True(read.Has(App, 1, 100, "Data\\Skyrim.esm"));
    }

    /// <summary>
    ///     The case the whole class exists for: somebody else's build is already in the shard this run is
    ///     writing to, and it has to still be there afterwards.
    /// </summary>
    [Fact]
    public async Task AnotherRunAddsToAShardRatherThanReplacingIt()
    {
        var hash = Hash.FromHex("cd00000000000000");
        var other = Hash.FromHex("cd11111111111111");

        var first = await Load();
        first.Add(File(hash, "Data\\Skyrim.esm", 1, 100));
        await first.Save(CancellationToken.None);

        var second = await Load();
        second.Add(File(other, "Data\\Dawnguard.esm", 2, 200));
        await second.Save(CancellationToken.None);

        var read = await Load();
        Assert.Equal(2, read.FileCount);
        Assert.True(read.Has(App, 1, 100, "Data\\Skyrim.esm"));
        Assert.True(read.Has(App, 2, 200, "Data\\Dawnguard.esm"));
    }

    /// <summary>
    ///     A file that did not change between two builds is in both of their manifests, and the index says
    ///     so: one hash, two places to fetch it from. That redundancy is what makes a repair likely to find
    ///     a copy it can actually open.
    /// </summary>
    [Fact]
    public async Task TheSameFileInTwoBuildsIsTwoEntries()
    {
        var hash = Hash.FromHex("ef00000000000000");

        var index = await Load();
        index.Add(File(hash, "Data\\Skyrim.esm", 1, 100));
        index.Add(File(hash, "Data\\Skyrim.esm", 1, 200));
        await index.Save(CancellationToken.None);

        var read = await Load();
        Assert.Equal(2, read.FileCount);
        Assert.True(read.Has(App, 1, 100, "Data\\Skyrim.esm"));
        Assert.True(read.Has(App, 1, 200, "Data\\Skyrim.esm"));
    }

    /// <summary>
    ///     Recording the same file twice is one entry, not two: a re-run with <c>--force</c> re-reads a
    ///     manifest it has already read, and the index must not grow every time somebody does that.
    /// </summary>
    [Fact]
    public async Task RecordingAFileTwiceKeepsOneEntry()
    {
        var hash = Hash.FromHex("1200000000000000");

        var index = await Load();
        index.Add(File(hash, "Data\\Skyrim.esm", 1, 100));
        index.Add(File(hash, "Data\\Skyrim.esm", 1, 100));
        await index.Save(CancellationToken.None);

        Assert.Equal(1, (await Load()).FileCount);
    }

    /// <summary>
    ///     Which manifests have been read all the way through, so a second run skips them. Recorded
    ///     separately from the files because a half-read manifest has files in the index and is not done.
    /// </summary>
    [Fact]
    public async Task AReadManifestIsRememberedAcrossRuns()
    {
        var index = await Load();
        index.Add(File(Hash.FromHex("3400000000000000"), "Data\\Skyrim.esm", 1, 100));
        index.RecordManifest(new IndexedManifest
        {
            App = App, Depot = 1, Manifest = 100, Version = "1.6.1170.0", Files = 1, Indexed = DateTime.UtcNow
        });
        await index.Save(CancellationToken.None);

        var read = await Load();
        Assert.True(read.HasManifest(App, 1, 100));
        Assert.False(read.HasManifest(App, 1, 200));
        Assert.Equal("1.6.1170.0", Assert.Single(read.Manifests).Version);
    }

    /// <summary>
    ///     Files written before the manifest was recorded are still there, which is what lets an
    ///     interrupted run pick up where it stopped instead of fetching a game again.
    /// </summary>
    [Fact]
    public async Task AnInterruptedRunKeepsWhatItAlreadyHashed()
    {
        var index = await Load();
        index.Add(File(Hash.FromHex("5600000000000000"), "Data\\Skyrim.esm", 1, 100));
        await index.Save(CancellationToken.None);

        var resumed = await Load();

        Assert.True(resumed.Has(App, 1, 100, "Data\\Skyrim.esm"));
        Assert.False(resumed.HasManifest(App, 1, 100));
        Assert.False(resumed.Has(App, 1, 100, "Data\\Dawnguard.esm"));
    }

    /// <summary>Paths come out of manifests, and a manifest is not careful about case.</summary>
    [Fact]
    public async Task APathIsMatchedWhateverItsCase()
    {
        var index = await Load();
        index.Add(File(Hash.FromHex("7800000000000000"), "Data\\Skyrim.esm", 1, 100));
        await index.Save(CancellationToken.None);

        Assert.True((await Load()).Has(App, 1, 100, "data\\skyrim.ESM"));
    }

    private Task<GameFileIndexFolder> Load()
    {
        return GameFileIndexFolder.Load(_root, Game.SkyrimSpecialEdition, _dtos, CancellationToken.None);
    }

    private AbsolutePath Shard(string shard)
    {
        return _root.Combine(Game.SkyrimSpecialEdition.ToString(), GameFileIndex.ContentFolder, $"{shard}.json");
    }

    private static IndexedGameFile File(Hash hash, string path, uint depot, ulong manifest)
    {
        return new IndexedGameFile
            {Hash = hash, Size = 42, Path = path, App = App, Depot = depot, Manifest = manifest};
    }
}
