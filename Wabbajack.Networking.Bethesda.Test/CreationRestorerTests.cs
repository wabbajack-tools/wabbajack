using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The Bethesda game file source, against a fake chain: no Steam client, no ticket, no network.
///     <para>
///         The two things worth pinning hardest are that one container serves both of a Creation's files -
///         without which every Creation in a repair is downloaded twice - and that each way of not having a
///         file gets the outcome that says what the user should do about it.
///     </para>
/// </summary>
public class CreationRestorerTests : IDisposable
{
    /// <summary>A real row out of the shipped table, so the stem matching is the real thing.</summary>
    private const long ArcaneArcher = 5648;

    private const string Plugin = "ccBGSSSE002-ExoticArrows.esl";
    private const string Archive = "ccBGSSSE002-ExoticArrows.bsa";

    private readonly FakeApi _api;
    private readonly CreationCache _cache;
    private readonly FakeDownloads _downloads;
    private readonly FakeGameLocator _locator;
    private readonly FakeSteamPresence _steam;
    private readonly TemporaryFileManager _temp;

    public CreationRestorerTests()
    {
        // A root outside the default "temp" one, not just a folder inside it. Test classes run in parallel
        // and a default TemporaryFileManager deletes the whole of "temp" when it is disposed, which took
        // these files with it.
        _temp = new TemporaryFileManager(KnownFolders.EntryPoint.Combine("creations-test",
            Guid.NewGuid().ToString("N")));
        _api = new FakeApi();
        _downloads = new FakeDownloads();
        _locator = new FakeGameLocator {Installed = true};
        _steam = new FakeSteamPresence {IsRunning = true};
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

    /// <summary>A container shaped like the real ones: the plugin and its archive, and nothing else.</summary>
    private static byte[] Container(string pluginText = "plugin bytes", string archiveText = "archive bytes")
    {
        return new BtarBuilder()
            .With($"Data\\{Plugin}", pluginText)
            .With($"Data\\{Archive}", archiveText)
            .Build();
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    private void Publish(byte[] container, string? checksum = null)
    {
        _downloads.Body = container;
        _api.Slots.Add(new CkmSlot(ArcaneArcher, "Arcane Archer Pack",
            new Uri($"https://ugcmods.bethesda.net/public/SKYRIM/client/{ArcaneArcher}/CSV2/CSV2.ckm?versionId=x"),
            checksum ?? Md5(container), container.Length));
    }

    private Task<GameFileRestoreResult> Restore(string fileName, string? version = null)
    {
        return Restorer.Restore(Game.SkyrimSpecialEdition, version, $"Data\\{fileName}".ToRelativePath(),
            _temp.CreateFolder().Path.Combine(fileName), CancellationToken.None);
    }

    [Fact]
    public void ItSaysWhereTheFilesComeFrom()
    {
        Assert.Equal("Bethesda", Restorer.SourceName);
    }

    [Fact]
    public async Task AGameWithNoCreationTableHasNoSourceHere()
    {
        var result = await Restorer.Restore(Game.Fallout4, null, "Data\\Whatever.esl".ToRelativePath(),
            _temp.CreateFolder().Path.Combine("Whatever.esl"), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.NoSource, result.Outcome);
        Assert.Equal(0, _downloads.Requests);
    }

    [Fact]
    public async Task AFileThatIsNotACreationIsNotFound()
    {
        var result = await Restore("Skyrim.esm");

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains("not one of the 74", result.Detail!);
        Assert.Equal(0, _api.SignIns);
    }

    /// <summary>
    ///     The whole point of this path: the ticket comes from the Steam client the user is already running,
    ///     so that - and not a Wabbajack login - is what the row has to ask for.
    /// </summary>
    [Fact]
    public void StatusAsksForARunningSteamClientAndNotForAWabbajackLogin()
    {
        Assert.True(Restorer.Status().Ready);

        _steam.IsRunning = false;
        var status = Restorer.Status();

        Assert.False(status.Ready);
        Assert.Contains("Start Steam", status.Reason);
        Assert.Contains("does not need a Wabbajack Steam login", status.Reason);
    }

    [Fact]
    public void StatusNeedsTheGameInstalled()
    {
        _locator.Installed = false;

        var status = Restorer.Status();

        Assert.False(status.Ready);
        Assert.Contains("Install Skyrim Special Edition", status.Reason);
    }

    [Fact]
    public async Task WithNoSteamRunningNothingIsAttempted()
    {
        Publish(Container());
        _steam.IsRunning = false;

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
        Assert.Equal(0, _downloads.Requests);
    }

    /// <summary>
    ///     A file Bethesda was never going to have must not produce an instruction to go and start Steam:
    ///     the user would do it and get the same answer.
    /// </summary>
    [Fact]
    public async Task ANonCreationIsAnsweredBeforeSteamIsAskedAbout()
    {
        _steam.IsRunning = false;

        var result = await Restore("Skyrim.esm");

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
    }

    [Fact]
    public async Task ACreationIsFetchedUnpackedAndWritten()
    {
        Publish(Container());
        var destination = _temp.CreateFolder().Path.Combine(Plugin);

        var result = await Restorer.Restore(Game.SkyrimSpecialEdition, null, $"Data\\{Plugin}".ToRelativePath(),
            destination, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal("plugin bytes", await destination.ReadAllTextAsync());
        Assert.Contains("Arcane Archer Pack", result.Detail!);
    }

    /// <summary>
    ///     The reason the cache is required rather than an optimisation. One <c>.ckm</c> holds two files and
    ///     <c>Restore</c> is asked for one at a time, so without somewhere to keep the other every Creation
    ///     in a repair is fetched twice.
    /// </summary>
    [Fact]
    public async Task OneDownloadServesBothOfACreationsFiles()
    {
        Publish(Container());

        var plugin = _temp.CreateFolder().Path.Combine(Plugin);
        var archive = _temp.CreateFolder().Path.Combine(Archive);

        var first = await Restorer.Restore(Game.SkyrimSpecialEdition, null, $"Data\\{Plugin}".ToRelativePath(),
            plugin, CancellationToken.None);
        var second = await Restorer.Restore(Game.SkyrimSpecialEdition, null, $"Data\\{Archive}".ToRelativePath(),
            archive, CancellationToken.None);

        Assert.True(first.Fetched);
        Assert.True(second.Fetched);
        Assert.Equal("plugin bytes", await plugin.ReadAllTextAsync());
        Assert.Equal("archive bytes", await archive.ReadAllTextAsync());

        Assert.Equal(1, _downloads.Requests);
        Assert.Equal(1, _cache.Downloads);
    }

    /// <summary>The <c>.bsa</c> and the <c>.esl</c> share a stem, and only the plugin is in the table.</summary>
    [Fact]
    public async Task AnArchiveResolvesToTheSameCreationAsItsPlugin()
    {
        Publish(Container());

        var result = await Restore(Archive);

        Assert.True(result.Fetched, result.Detail);
        Assert.Contains(ArcaneArcher.ToString(), result.Detail!);
    }

    [Fact]
    public async Task TheTicketIsMintedAndTheContentResolvedOncePerRun()
    {
        Publish(Container());

        await Restore(Plugin);
        await Restore(Archive);

        Assert.Equal(1, _api.SignIns);
        Assert.Equal(1, _api.Resolves);
    }

    /// <summary>One request answers for every Creation there is, so it is asked about every Creation.</summary>
    [Fact]
    public async Task TheResolveAsksAboutTheWholeTable()
    {
        Publish(Container());

        await Restore(Plugin);

        Assert.Equal(CreationIndex.Default.ContentIds.Count, _api.Asked.Count);
        Assert.Contains(ArcaneArcher, _api.Asked);
    }

    /// <summary>
    ///     A record that never came back is absence and nothing more. It is what a Creation the account does
    ///     not own is expected to look like, and equally what one Bethesda has stopped publishing looks like,
    ///     so the message offers ownership as a possibility rather than stating it.
    /// </summary>
    [Fact]
    public async Task ACreationBethesdaResolvedNothingForIsNotFound()
    {
        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains("may not own", result.Detail!);
        Assert.Equal(0, _downloads.Requests);
    }

    [Fact]
    public async Task AContainerThatDoesNotMatchItsChecksumIsNotUnpacked()
    {
        Publish(Container(), new string('a', 32));

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("not the", result.Detail!);
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task AContainerThatDoesNotHoldTheWantedFileIsNotFound()
    {
        var container = new BtarBuilder().With("Data\\SomethingElse.esl", "nope").Build();
        Publish(container);

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains("SomethingElse.esl", result.Detail!);
    }

    [Fact]
    public async Task AContainerThatIsNotABtarIsAFailure()
    {
        Publish(Encoding.ASCII.GetBytes("this is not a container"));

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task AStorageRefusalIsAFailure()
    {
        Publish(Container());
        _downloads.Status = HttpStatusCode.Forbidden;

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
    }

    /// <summary>
    ///     Everything a missing ticket means - Steam closed, nobody signed in, the game's library gone - is
    ///     something the user can go and fix, so it reads as "not set up" rather than as a failure of this
    ///     file.
    /// </summary>
    [Fact]
    public async Task ATicketThatCannotBeMintedReadsAsNotSetUp()
    {
        Publish(Container());
        _api.SignInThrows = () => new SteamAppTicketException(SteamAppTicketError.SteamNotRunning,
            "Steam is not running.");

        var result = await Restore(Plugin);

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
        Assert.Equal("Steam is not running.", result.Detail!);
    }

    /// <summary>
    ///     Creations are not published per game build, so a version is neither honoured nor reported back.
    ///     Answering <c>VersionUnknown</c> would be worse than ignoring it: <c>GameFileRepair</c> ranks that
    ///     as its most informative failure and would tell the user to go and find a version index for
    ///     something that has no versions.
    /// </summary>
    [Fact]
    public async Task AVersionIsNeitherHonouredNorReported()
    {
        Publish(Container());

        var result = await Restore(Plugin, "1.5.97.0");

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Null(result.Version);
    }

    [Fact]
    public void ConsequencesAreSaidOnlyForAGameThatHasCreations()
    {
        Assert.Empty(Restorer.Consequences(new[] {Game.Fallout4, Game.Skyrim}));

        var said = Restorer.Consequences(new[] {Game.Fallout4, Game.SkyrimSpecialEdition});

        Assert.Single(said);
        Assert.Contains("Anniversary Upgrade", said[0]);
        Assert.Contains("No Wabbajack Steam login is needed", said[0]);
    }

    /// <summary>
    ///     The bound has to clear a whole repair's working set, which here is however many Creations the
    ///     repair touches - at most every one there is. A smaller bound evicts between a Creation's two
    ///     files, which is the failure the Steam manifest cache already had.
    /// </summary>
    [Fact]
    public void TheCacheHoldsAWholeRepairsWorkingSet()
    {
        Assert.Equal(CreationIndex.ExpectedCount, CreationCache.MaxCachedCreations);
    }

    [Fact]
    public async Task AnUnpackedCreationIsHeldForTheRun()
    {
        Publish(Container());

        await Restore(Plugin);

        Assert.Equal(1, _cache.Count);
    }

    /// <summary>Answers from a script, and counts what it was asked.</summary>
    private sealed class FakeApi : BethesdaApiClient
    {
        public FakeApi() : base(new HttpClient(), new NoTicket(), NullLogger<BethesdaApiClient>.Instance)
        {
        }

        public List<CkmSlot> Slots { get; } = new();
        public List<long> Asked { get; } = new();
        public int SignIns { get; private set; }
        public int Resolves { get; private set; }
        public Func<Exception>? SignInThrows { get; set; }

        public override Task SignIn(CancellationToken token = default)
        {
            SignIns++;
            if (SignInThrows != null) throw SignInThrows();
            return Task.CompletedTask;
        }

        public override Task<IReadOnlyList<CkmSlot>> Resolve(IEnumerable<long> contentIds,
            CancellationToken token = default)
        {
            Resolves++;
            Asked.AddRange(contentIds);
            return Task.FromResult<IReadOnlyList<CkmSlot>>(Slots.ToArray());
        }
    }

    /// <summary>Serves one container, and counts how many times it was asked for it.</summary>
    private sealed class FakeDownloads : HttpMessageHandler
    {
        public byte[] Body { get; set; } = Array.Empty<byte>();
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(Status) {Content = new ByteArrayContent(Body)});
        }
    }

    private sealed class FakeGameLocator : IGameLocator
    {
        public bool Installed { get; set; }

        public AbsolutePath GameLocation(Game game)
        {
            return KnownFolders.EntryPoint;
        }

        public bool IsInstalled(Game game)
        {
            return Installed;
        }

        public bool TryFindLocation(Game game, out AbsolutePath path)
        {
            path = Installed ? KnownFolders.EntryPoint : default;
            return Installed;
        }

        public bool TryGetSteamBuildId(Game game, out string buildId)
        {
            buildId = string.Empty;
            return false;
        }
    }

    private sealed class FakeSteamPresence : ISteamClientPresence
    {
        public bool IsRunning { get; set; }
    }

    private sealed class NoTicket : ISteamAppTicketSource
    {
        public ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default)
        {
            throw new SteamAppTicketException(SteamAppTicketError.SteamNotRunning, "No Steam here.");
        }
    }
}
