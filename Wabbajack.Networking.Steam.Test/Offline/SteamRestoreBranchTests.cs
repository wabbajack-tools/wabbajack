using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SteamKit2;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Networking.Steam.Test.Offline;

/// <summary>
///     The branches of <see cref="SteamGameFileRestorer.Restore" /> that <c>SteamGameFileRestorerTests</c>
///     does not reach: how each kind of refusal is sorted into "try the next depot" or "stop and say so",
///     what is said when nothing is published, and the content-index rules that keep a repair from adding a
///     licence to somebody's Steam library that it did not need.
/// </summary>
public class SteamRestoreBranchTests
{
    private const uint SkyrimSE = 489830;
    private const uint SkyrimSECreationKit = 1946180;
    private const string Version = "1.6.640.0";

    private static readonly AbsolutePath Output = "c:\\out\\Skyrim.esm".ToAbsolutePath();

    private readonly FakeContentClient _content = new();
    private readonly FakeIndex _index = new();
    private readonly FakeSession _session = new() {IsLoggedIn = true};

    private SteamGameFileRestorer Restorer()
    {
        return new SteamGameFileRestorer(NullLogger<SteamGameFileRestorer>.Instance, _session, _content, _index);
    }

    private Task<GameFileRestoreResult> Restore(string? version, string file = "Data/Skyrim.esm",
        GameFileIdentity? wanted = null, CancellationToken token = default, Game game = Game.SkyrimSpecialEdition)
    {
        return Restorer().Restore(game, version, file.ToRelativePath(), Output, token, wanted);
    }

    /// <summary>Two depots recorded for <see cref="Version" />, the second of which carries the file.</summary>
    private void TwoDepotsAndTheSecondHasIt()
    {
        _index.Versions[Version] = new[]
        {
            new SteamManifest {Depot = 1, Manifest = 100},
            new SteamManifest {Depot = 2, Manifest = 200}
        };
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Skyrim.esm"};
    }

    /// <summary>
    ///     A licence list that never arrived is reported as exactly that. The user must not read "holds no
    ///     licence" and go off to buy a game they own because Steam was slow to answer.
    /// </summary>
    [Fact]
    public async Task AnUnconfirmedLicenceIsReportedAsUnknownRatherThanNotOwned()
    {
        _index.Versions[Version] = new[] {new SteamManifest {Depot = 1, Manifest = 100}};
        _content.Unconfirmed.Add(1);

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("says nothing about whether you own", result.Detail);
        Assert.DoesNotContain("holds no licence", result.Detail);
    }

    /// <summary>
    ///     A manifest Steam has stopped serving is what an old game version eventually becomes. It says
    ///     nothing about the next depot, so the search carries on and the file is still fetched.
    /// </summary>
    [Fact]
    public async Task AManifestSteamNoLongerServesFallsThroughToTheNextDepot()
    {
        TwoDepotsAndTheSecondHasIt();
        _content.FindThrows[1] = () => new SteamManifestUnavailableException("no request code", SkyrimSE, 1, 100);

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>A depot key Steam refuses with AccessDenied is the same answer, and is treated the same.</summary>
    [Fact]
    public async Task AnAccessDeniedDepotFallsThroughToTheNextDepot()
    {
        TwoDepotsAndTheSecondHasIt();
        _content.FindThrows[1] = () => new SteamException("depot key", EResult.AccessDenied, EResult.AccessDenied);

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     Any other Steam result is not a refusal of this depot, so it is not swallowed as one: the search
    ///     stops and the user is told what broke.
    /// </summary>
    [Fact]
    public async Task ASteamErrorOtherThanAccessDeniedStopsTheSearch()
    {
        TwoDepotsAndTheSecondHasIt();
        _content.FindThrows[1] = () => new SteamException("timed out", EResult.Timeout, EResult.Timeout);

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("timed out", result.Detail);
        Assert.Equal(new[] {(SkyrimSE, 1u, 100ul)}, _content.FindAttempts);
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>
    ///     Something that has nothing to do with entitlement - a full disk, a broken connection - ends the
    ///     search there, and the depots after it are not read. A repair that has started failing like this
    ///     would only repeat the same failure on every remaining depot.
    /// </summary>
    [Fact]
    public async Task AnyOtherFailureStopsTheSearchAndLaterDepotsAreNotRead()
    {
        TwoDepotsAndTheSecondHasIt();
        _content.FindThrows[1] = () => new IOException("disk full");

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Equal("disk full", result.Detail);
        Assert.Equal(Version, result.Version);
        Assert.DoesNotContain(_content.FindAttempts, a => a.Depot == 2);
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>
    ///     An app that publishes no depots at all has nowhere to fetch from. That is "no source", which lets
    ///     a second source try, and not "file not found", which would claim a search that never happened.
    /// </summary>
    [Fact]
    public async Task AnAppThatPublishesNothingHasNoSource()
    {
        var result = await Restore(null, "Fallout3.exe", game: Game.Fallout3);

        Assert.Equal(GameFileRestoreOutcome.NoSource, result.Outcome);
        Assert.Contains("publishes nothing on its public branch", result.Detail);
        Assert.Contains("app 22300", result.Detail);
        Assert.Null(result.Version);
    }

    /// <summary>
    ///     When a version was asked for, a wrong-size copy is described as the copy recorded for that
    ///     version, not "now": the user is looking at a list built for an older build and needs to see that.
    /// </summary>
    [Fact]
    public async Task AWrongSizedCopyOfARecordedVersionNamesTheVersion()
    {
        _index.Versions[Version] = new[] {new SteamManifest {Depot = 1, Manifest = 100}};
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Sizes["Data\\Skyrim.esm"] = 500;

        var result = await Restore(Version, wanted: new GameFileIdentity(new Hash(0xDEADBEEF), 400));

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains($"for {Version}", result.Detail);
        Assert.DoesNotContain(" now ", result.Detail);
        Assert.Contains("500", result.Detail);
        Assert.Contains("400", result.Detail);
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>
    ///     A saved login that Steam turns down is "log in again", said before anything else is touched -
    ///     no index lookups and no depot reads made on behalf of a login that does not work.
    /// </summary>
    [Fact]
    public async Task ASavedLoginThatFailsToRedeemIsNotReadyAndNothingIsAsked()
    {
        _session.IsLoggedIn = false;
        _session.HaveStoredToken = true;
        _session.StoredLoginThrows = () => new SteamLoginRequiredException("Your saved Steam login has expired.");
        TwoDepotsAndTheSecondHasIt();

        var result = await Restore(Version, wanted: new GameFileIdentity(new Hash(0x1234), 1));

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
        Assert.Equal("Your saved Steam login has expired.", result.Detail);
        Assert.Empty(_index.Looked);
        Assert.Empty(_index.Asked);
        Assert.Empty(_content.FindAttempts);
        Assert.Empty(_content.FreeLicensesAsked);
    }

    /// <summary>A repair the user cancelled stops, rather than being reported as a failed file.</summary>
    [Fact]
    public async Task ACancelledRestoreThrowsRatherThanFailing()
    {
        TwoDepotsAndTheSecondHasIt();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Restore(Version, token: cancelled.Token));
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>A cancellation that arrives while a manifest is being read is not swallowed as a failure either.</summary>
    [Fact]
    public async Task ACancellationDuringAManifestReadThrows()
    {
        TwoDepotsAndTheSecondHasIt();
        _content.FindThrows[1] = () => new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Restore(Version));
        Assert.DoesNotContain(_content.FindAttempts, a => a.Depot == 2);
    }

    /// <summary>
    ///     Pins what happens today when the version index itself cannot be read (anything but a 404, which
    ///     <c>ClientSteamManifestIndex</c> turns into "no record"): the repair of that file fails with the
    ///     index's own error, and no depot is searched. Unlike the content index, there is no fallback.
    /// </summary>
    [Fact]
    public async Task AVersionIndexThatCannotBeReadFailsTheFile()
    {
        _index.GetThrows = () => new InvalidOperationException("the version index is unreachable");
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restore(Version);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Equal("the version index is unreachable", result.Detail);
        Assert.Equal(Version, result.Version);
        Assert.Empty(_content.FindAttempts);
        Assert.Empty(_content.FreeLicensesAsked);
    }

    /// <summary>
    ///     The content index can name a copy under any app id, but only the game's own apps and its
    ///     companion apps (<c>SteamToolIDs</c>) belong to this repair. A copy under some other app must not
    ///     cause a licence request for that app - which would add an unrelated package to the user's Steam
    ///     library - and must not be read. The repair should carry on to the game's own depots.
    /// </summary>
    [Fact]
    public async Task AContentIndexCopyUnderAnUnrelatedAppIsNeitherLicensedNorRead()
    {
        const uint unrelatedApp = 12345;
        var hash = new Hash(0x4242);
        _index.Content[hash] = new[]
        {
            new IndexedGameFile
            {
                Hash = hash, Size = 1, App = unrelatedApp, Depot = 12346, Manifest = 600, Path = "Data\\Skyrim.esm"
            }
        };
        _content.Manifests[(12346u, 600ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restore(null, wanted: new GameFileIdentity(hash, 1));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.DoesNotContain(unrelatedApp, _content.FreeLicensesAsked);
        Assert.DoesNotContain(_content.Downloaded, d => d.App == unrelatedApp);
        Assert.Equal((SkyrimSE, 1u, 100ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     The rule the depot search follows has to hold for the content index too: when the game's own
    ///     depot carries the file, the Creation Kit's copy is never reached, so no Creation Kit licence is
    ///     added to the user's library for a file they could have had without it.
    /// </summary>
    [Fact]
    public async Task AContentIndexCopyInTheGamesOwnDepotIsPreferredToAToolCopy()
    {
        var hash = new Hash(0x4343);
        _index.Content[hash] = new[]
        {
            new IndexedGameFile
            {
                Hash = hash, Size = 1, App = SkyrimSECreationKit, Depot = 1946182, Manifest = 500,
                Path = "Data\\Scripts.zip"
            },
            new IndexedGameFile
                {Hash = hash, Size = 1, App = SkyrimSE, Depot = 9, Manifest = 900, Path = "Data\\Scripts.zip"}
        };
        _content.GrantsFreeLicense.Add(SkyrimSECreationKit);
        _content.Manifests[(1946182u, 500ul)] = new[] {"Data\\Scripts.zip"};
        _content.Manifests[(9u, 900ul)] = new[] {"Data\\Scripts.zip"};

        var result = await Restore(null, "Data/Scripts.zip", new GameFileIdentity(hash, 1));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Empty(_content.FreeLicensesAsked);
        Assert.Equal((SkyrimSE, 9u, 900ul, "Data\\Scripts.zip"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     What does work today: a content-index copy under the game's companion app takes that app's free
    ///     licence on the way in, the same as the depot search does, and nothing is asked of the game's own app.
    /// </summary>
    [Fact]
    public async Task AContentIndexCopyUnderTheCreationKitTakesItsLicenceOnTheWayIn()
    {
        var hash = new Hash(0x4444);
        _index.Content[hash] = new[]
        {
            new IndexedGameFile
            {
                Hash = hash, Size = 1, App = SkyrimSECreationKit, Depot = 1946182, Manifest = 500,
                Path = "CreationKit.exe"
            }
        };
        _content.GrantsFreeLicense.Add(SkyrimSECreationKit);
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restore(null, "CreationKit.exe", new GameFileIdentity(hash, 1));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(new[] {SkyrimSECreationKit}, _content.FreeLicensesAsked);
        Assert.Contains("from the content index", result.Detail);
        Assert.DoesNotContain(_content.FindAttempts, a => a.App == SkyrimSE);
    }
}
