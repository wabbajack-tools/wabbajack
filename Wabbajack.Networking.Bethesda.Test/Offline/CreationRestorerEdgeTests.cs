using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test.Offline;

/// <summary>
///     The parts of <see cref="CreationRestorer" /> that <c>CreationRestorerTests</c> leaves out: that it never
///     claims to stand in for a missing game, that a modlist's casing does not cost a user their Creation,
///     that it makes the folder it writes into, and that a cancelled repair stops rather than failing a file.
/// </summary>
public class CreationRestorerEdgeTests : IDisposable
{
    private const long ArcaneArcher = 5648;
    private const string Plugin = "ccBGSSSE002-ExoticArrows.esl";
    private const string Archive = "ccBGSSSE002-ExoticArrows.bsa";

    private readonly FakeApi _api = new();
    private readonly CreationCache _cache;
    private readonly FakeDownloads _downloads = new();
    private readonly FakeGameLocator _locator = new() {Installed = true};
    private readonly FakeSteamPresence _steam = new() {IsRunning = true};
    private readonly TemporaryFileManager _temp;

    public CreationRestorerEdgeTests()
    {
        _temp = new TemporaryFileManager(KnownFolders.EntryPoint.Combine("creation-edge-test",
            Guid.NewGuid().ToString("N")));
        _cache = new CreationCache(NullLogger<CreationCache>.Instance, _api, CreationIndex.Default,
            new HttpClient(_downloads), _temp);
    }

    private CreationRestorer Restorer => new(NullLogger<CreationRestorer>.Instance, CreationIndex.Default,
        _cache, _locator, _steam);

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
        _downloads.Dispose();
    }

    private void Publish()
    {
        var container = new BtarBuilder()
            .With($"Data\\{Plugin}", "plugin bytes")
            .With($"Data\\{Archive}", "archive bytes")
            .Build();
        _downloads.Body = container;
        _api.Slots.Add(new CkmSlot(ArcaneArcher, "Arcane Archer Pack",
            new Uri($"https://ugcmods.bethesda.net/public/SKYRIM/client/{ArcaneArcher}/CSV2/CSV2.ckm?versionId=x"),
            Convert.ToHexString(MD5.HashData(container)).ToLowerInvariant(), container.Length));
    }

    /// <summary>
    ///     Creations are add-ons, not the game. Whatever the account owns, this source never tells preflight
    ///     it can stand in for a missing Skyrim install - and it asks nothing of Steam or Bethesda to say so.
    /// </summary>
    [Theory]
    [InlineData(Game.SkyrimSpecialEdition)]
    [InlineData(Game.Fallout4)]
    public async Task ItNeverStandsInForAMissingGame(Game game)
    {
        var result = await Restorer.CanSourceGame(game, CancellationToken.None);

        Assert.Equal(GameSourceOutcome.NoSource, result.Outcome);
        Assert.Contains("not the game itself", result.Reason);
        Assert.Equal(0, _api.SignIns);
        Assert.Equal(0, _downloads.Requests);
    }

    /// <summary>
    ///     A modlist may record a Creation's file in any casing. It is still that Creation, and the file is
    ///     still found inside the container.
    /// </summary>
    [Fact]
    public async Task AFileIsMatchedWhateverItsCasing()
    {
        Publish();
        var output = _temp.CreateFolder().Path.Combine("out.esl");

        var result = await Restorer.Restore(Game.SkyrimSpecialEdition, null,
            "data\\CCBGSSSE002-EXOTICARROWS.ESL".ToRelativePath(), output, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal("plugin bytes", await output.ReadAllTextAsync());
    }

    /// <summary>The folder the file is written into is created if it is not there yet.</summary>
    [Fact]
    public async Task TheOutputFolderIsCreated()
    {
        Publish();
        var output = _temp.CreateFolder().Path.Combine("nested", "deeper", Plugin);
        Assert.False(output.Parent.DirectoryExists());

        var result = await Restorer.Restore(Game.SkyrimSpecialEdition, null, $"Data\\{Plugin}".ToRelativePath(),
            output, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.True(output.FileExists());
    }

    /// <summary>A repair the user cancelled stops, rather than being reported as a Creation that failed.</summary>
    [Fact]
    public async Task ACancelledRestoreThrowsRatherThanFailing()
    {
        Publish();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Restorer.Restore(Game.SkyrimSpecialEdition,
            null, $"Data\\{Plugin}".ToRelativePath(), _temp.CreateFolder().Path.Combine(Plugin), cancelled.Token));
        Assert.Equal(0, _downloads.Requests);
    }

    /// <summary>A cancellation raised from inside the chain is passed up the same way.</summary>
    [Fact]
    public async Task ACancellationFromSigningInIsPassedUp()
    {
        Publish();
        _api.SignInThrows = () => new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Restorer.Restore(Game.SkyrimSpecialEdition,
            null, $"Data\\{Plugin}".ToRelativePath(), _temp.CreateFolder().Path.Combine(Plugin),
            CancellationToken.None));
    }
}
