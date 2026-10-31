using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SteamKit2;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Turning "this file of this game at this version" into an app, a depot and a manifest. Everything here
///     runs against a stand-in content client and a stand-in index, so nothing touches Steam or the network:
///     what is under test is which questions get asked and in what order, not the fetching itself.
/// </summary>
public class SteamGameFileRestorerTests
{
    private const uint SkyrimSE = 489830;

    /// <summary>Skyrim Special Edition: Creation Kit, the one entry in the game's <c>SteamToolIDs</c>.</summary>
    private const uint SkyrimSECreationKit = 1946180;

    private const string Version = "1.6.640.0";

    private readonly FakeContentClient _content = new();
    private readonly FakeIndex _index = new();
    private readonly FakeSession _session = new();

    private SteamGameFileRestorer Restorer()
    {
        return new SteamGameFileRestorer(NullLogger<SteamGameFileRestorer>.Instance, _session, _content, _index);
    }

    private static RelativePath File(string path)
    {
        return path.ToRelativePath();
    }

    /// <summary>
    ///     What a caller knows about the file it wants. The hash defaults to one nothing is indexed under,
    ///     so a test about sizes falls through the content index the way an unindexed game does.
    /// </summary>
    private static GameFileIdentity Wanted(long size, Hash? hash = null)
    {
        return new GameFileIdentity(hash ?? new Hash(0xDEADBEEF), size);
    }

    [Fact]
    public void WithNoStoredLoginItSaysWhatOneWouldBuy()
    {
        var status = Restorer().Status();

        Assert.False(status.Ready);
        Assert.Contains("Log into Steam", status.Reason);
        Assert.Contains("never written to", status.Reason);
    }

    /// <summary>
    ///     A game with no companion app says nothing extra, because nothing extra happens: its depots are
    ///     read with the licence the user already has and their library is untouched.
    /// </summary>
    [Fact]
    public void AGameWithNoCompanionAppAddsNothingToTheLibrary()
    {
        Assert.Empty(Game.Fallout3.MetaData().SteamToolIDs);
        Assert.Empty(Restorer().Consequences(new[] {Game.Fallout3}));
    }

    /// <summary>
    ///     A game with one says so, and names it. <c>SteamToolIDs</c> is the condition and not
    ///     <c>SteamIDs</c>: reading the latter would answer for the game's own app, which the account
    ///     already holds, and so would never fire at all.
    /// </summary>
    [Fact]
    public void AGameWithACompanionAppNamesItBeforeAnythingIsFetched()
    {
        var said = Assert.Single(Restorer().Consequences(new[] {Game.SkyrimSpecialEdition}));

        Assert.Contains("Creation Kit", said);
        Assert.Contains("Steam library", said);
        Assert.Contains("If one of them turns out to be needed", said);
    }

    /// <summary>One sentence for a repair spanning two games that share no companion app between them.</summary>
    [Fact]
    public void TwoGamesWithKitsAreOneSentence()
    {
        var said = Assert.Single(Restorer().Consequences(new[] {Game.SkyrimSpecialEdition, Game.Fallout4}));

        Assert.Contains("the Skyrim Special Edition Creation Kit", said);
        Assert.Contains("the Fallout 4 Creation Kit", said);
    }

    [Fact]
    public void ASavedLoginIsReadyWithoutBeingRedeemedFirst()
    {
        _session.HaveStoredToken = true;

        Assert.True(Restorer().Status().Ready);
        Assert.False(_session.LoggedInWithStoredToken);
    }

    [Fact]
    public async Task WithNoLoginNothingIsAsked()
    {
        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("Data/Skyrim.esm"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
        Assert.Empty(_content.Searched);
        Assert.Empty(_index.Asked);
    }

    [Fact]
    public async Task AGameThatIsNotOnSteamHasNoSource()
    {
        _session.IsLoggedIn = true;

        var result = await Restorer().Restore(Game.Fallout4London, Version, File("Data/Fallout4.esm"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.NoSource, result.Outcome);
    }

    /// <summary>
    ///     A saved login is redeemed on the way in, so a host that has one never has to think about the
    ///     session at all.
    /// </summary>
    [Fact]
    public async Task ASavedLoginIsRedeemedBeforeAnythingIsFetched()
    {
        _session.HaveStoredToken = true;
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.True(_session.LoggedInWithStoredToken);
    }

    /// <summary>
    ///     With no version wanted, the depots the app publishes today - no index lookup at all, which is the
    ///     whole of the "the user never installed it" case.
    /// </summary>
    [Fact]
    public async Task WithNoVersionItAsksTheGameWhatItPublishesNow()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Empty(_index.Asked);
        Assert.Equal(new[] {(SkyrimSE, 1u, 100ul), (SkyrimSE, 2u, 200ul)}, _content.Searched);
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Dawnguard.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     The content index is asked first when the caller said which bytes it wants, and its answer is
    ///     taken straight: the manifest it names is not one the game publishes now and no version was asked
    ///     for, so nothing else in this class could have found it. This is the case a list built against a
    ///     build Steam has moved past falls into.
    /// </summary>
    [Fact]
    public async Task TheContentIndexIsAskedFirstAndItsAnswerIsFetchedDirectly()
    {
        var hash = new Hash(0x1234567812345678);
        _session.IsLoggedIn = true;
        _index.Content[hash] = new[]
        {
            new IndexedGameFile
            {
                Hash = hash, Size = 42, App = SkyrimSE, Depot = 9, Manifest = 900, Path = "Data\\Dawnguard.esm"
            }
        };
        _content.Manifests[(9u, 900ul)] = new[] {"Data\\Dawnguard.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, "1.6.1170.0",
            File("Data/Dawnguard.esm"), "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None,
            Wanted(42, hash));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Contains("content index", result.Detail);
        Assert.Equal((SkyrimSE, 9u, 900ul, "Data\\Dawnguard.esm"), Assert.Single(_content.Downloaded));

        // Nothing else was asked: not the version index, not the depots the game publishes today.
        Assert.Empty(_index.Asked);
        Assert.Empty(_content.Searched);
    }

    /// <summary>
    ///     A file the index has nothing on falls through to the search it always did, and the index is asked
    ///     once rather than not at all - a game nobody has indexed must cost one 404 and no more.
    /// </summary>
    [Fact]
    public async Task AnUnindexedFileFallsBackToSearchingTheDepots()
    {
        var hash = new Hash(0xABCDEF);
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None, Wanted(1, hash));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(hash, Assert.Single(_index.Looked));
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Dawnguard.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     An index entry that cannot be read - a manifest Steam has stopped serving, a depot this account
    ///     cannot open - is one copy failing rather than the file being unobtainable, so the next copy and
    ///     then the ordinary search still happen.
    /// </summary>
    [Fact]
    public async Task ACopyTheIndexNamesThatCannotBeReadFallsThrough()
    {
        var hash = new Hash(0x5150);
        _session.IsLoggedIn = true;
        _content.Refuses.Add(9);
        _index.Content[hash] = new[]
        {
            new IndexedGameFile
                {Hash = hash, Size = 1, App = SkyrimSE, Depot = 9, Manifest = 900, Path = "Data\\Skyrim.esm"},
            new IndexedGameFile
                {Hash = hash, Size = 1, App = SkyrimSE, Depot = 8, Manifest = 800, Path = "Data\\Skyrim.esm"}
        };
        _content.Manifests[(9u, 900ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Manifests[(8u, 800ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None, Wanted(1, hash));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 8u, 800ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     The index is somebody's GitHub repo, so a repair cannot depend on it being up: one that cannot be
    ///     read is a search by version, not a failure.
    /// </summary>
    [Fact]
    public async Task AnIndexThatCannotBeReadIsNotAFailedRepair()
    {
        _session.IsLoggedIn = true;
        _index.FindThrows = () => new Exception("GitHub is having a day");
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None, Wanted(1));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Single(_content.Downloaded);
    }

    /// <summary>
    ///     A caller that says nothing about the file it wants asks nothing of the content index: there is
    ///     nothing to look up without a hash.
    /// </summary>
    [Fact]
    public async Task WithNoIdentityTheIndexIsNotAsked()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};

        await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Empty(_index.Looked);
    }

    /// <summary>
    ///     A copy of the file that is the wrong size is not the file being asked for, and the manifest says
    ///     so before a byte is fetched. This is the ordinary case for a list built against a game version
    ///     the store has moved past: the caller hashes everything that comes back and throws away what does
    ///     not match, so without this Skyrim's textures are downloaded to establish what their size already
    ///     said.
    /// </summary>
    [Fact]
    public async Task AFileOfAnotherSizeIsNotDownloadedAtAll()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};
        _content.Sizes["Data\\Dawnguard.esm"] = 25884488;

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None, Wanted(25884000));

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains("different file", result.Detail);
        Assert.Contains("25884488", result.Detail);
        Assert.Contains("25884000", result.Detail);

        // Searched, and deliberately not fetched.
        Assert.NotEmpty(_content.Searched);
        Assert.Empty(_content.Downloaded);
    }

    [Fact]
    public async Task AFileOfTheRightSizeIsFetchedAsUsual()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};
        _content.Sizes["Data\\Dawnguard.esm"] = 25884488;

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None, Wanted(25884488));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Single(_content.Downloaded);
    }

    /// <summary>
    ///     A caller that does not know how big the file should be gets what it always got: every manifest
    ///     carrying the name is a candidate.
    /// </summary>
    [Fact]
    public async Task WithNoExpectedSizeNothingIsPreChecked()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};
        _content.Sizes["Data\\Dawnguard.esm"] = 25884488;

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
    }

    /// <summary>
    ///     The size is checked per candidate rather than once: a depot that carries the wrong copy does not
    ///     say anything about the next one, and the file wanted may well be in it.
    /// </summary>
    [Fact]
    public async Task ADepotWithTheWrongCopyDoesNotStopTheSearch()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Sizes["1:Data\\Skyrim.esm"] = 10;
        _content.Sizes["2:Data\\Skyrim.esm"] = 20;

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None, Wanted(20));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     With a version, the depots and manifests the index recorded for it. Steam cannot be asked: its
    ///     client API only ever publishes the current build.
    /// </summary>
    [Fact]
    public async Task WithAVersionItResolvesThroughTheIndex()
    {
        _session.IsLoggedIn = true;
        _index.Versions[Version] = new[] {new SteamManifest {Depot = 7, Manifest = 700}};
        _content.Manifests[(7u, 700ul)] = new[] {"Data\\Skyrim.esm"};
        // What the app publishes today is a different manifest, and must not be the one used.
        _content.Current.Add(new DepotManifestId(7, 999));
        _content.Manifests[(7u, 999ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(Version, result.Version);
        Assert.Equal((Game.SkyrimSpecialEdition, Version), Assert.Single(_index.Asked));
        Assert.Equal((SkyrimSE, 7u, 700ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    [Fact]
    public async Task AVersionTheIndexDoesNotCarryIsSaidPlainly()
    {
        _session.IsLoggedIn = true;

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, "1.4.2.0", File("Data/Skyrim.esm"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.VersionUnknown, result.Outcome);
        Assert.Equal("1.4.2.0", result.Version);
        Assert.Contains("no record of", result.Detail);
        Assert.Contains("1.4.2.0", result.Detail);
        Assert.Empty(_content.Searched);
    }

    [Fact]
    public async Task AFileNoManifestCarriesIsNotFound()
    {
        _session.IsLoggedIn = true;
        _index.Versions[Version] = new[]
        {
            new SteamManifest {Depot = 1, Manifest = 100},
            new SteamManifest {Depot = 2, Manifest = 200}
        };
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Update.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("Data/Dawnguard.esm"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Contains("Data\\Dawnguard.esm", result.Detail);
        Assert.Equal(2, _content.Searched.Count);
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>
    ///     A depot the account cannot open says nothing about the next one, and the file may well be in that
    ///     one, so the search carries on rather than stopping at the first refusal.
    /// </summary>
    [Fact]
    public async Task ADepotThatRefusesDoesNotEndTheSearch()
    {
        _session.IsLoggedIn = true;
        _index.Versions[Version] = new[]
        {
            new SteamManifest {Depot = 1, Manifest = 100},
            new SteamManifest {Depot = 2, Manifest = 200}
        };
        _content.Refuses.Add(1);
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 2u, 200ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     When every depot refused and nothing was found, the refusal is what to report: "no manifest has
    ///     that file" would be a guess about a manifest nobody managed to read.
    /// </summary>
    [Fact]
    public async Task WhenEveryDepotRefusesTheRefusalIsWhatIsReported()
    {
        _session.IsLoggedIn = true;
        _index.Versions[Version] = new[] {new SteamManifest {Depot = 1, Manifest = 100}};
        _content.Refuses.Add(1);

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("Data/Skyrim.esm"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("holds no licence", result.Detail);
    }

    /// <summary>
    ///     A path is matched the way the depot spells it: a modlist records a game-relative path with
    ///     whatever separators and casing it was compiled with, a manifest records Valve's.
    /// </summary>
    [Fact]
    public async Task ThePathIsMatchedAsTheDepotSpellsIt()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"data\\SKYRIM.ESM"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal("data\\SKYRIM.ESM", Assert.Single(_content.Downloaded).Path);
    }

    /// <summary>
    ///     The Creation Kit is a Steam app of its own, but it installs into the game's folder, so a modlist
    ///     records <c>CreationKit.exe</c> as a file of Skyrim Special Edition. The game's own depots are
    ///     searched first and do not carry it; the Kit's do.
    /// </summary>
    [Fact]
    public async Task AFileTheGamesOwnDepotsDoNotCarryIsLookedForInTheCreationKits()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Publishes(SkyrimSECreationKit).Add(new DepotManifestId(1946182, 500));
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("CreationKit.exe"),
            "c:\\out\\CreationKit.exe".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(new[] {(SkyrimSE, 1u, 100ul), (SkyrimSECreationKit, 1946182u, 500ul)}, _content.Searched);
        Assert.Equal((SkyrimSECreationKit, 1946182u, 500ul, "CreationKit.exe"),
            Assert.Single(_content.Downloaded));
        Assert.Contains("app 1946180", result.Detail);
    }

    /// <summary>
    ///     The Kit is free but not licence-free, and Steam will not even describe the app to an account
    ///     holding nothing that names it. So the licence is asked for on the way in - and only for the
    ///     tool, never for the game, which the user bought.
    /// </summary>
    [Fact]
    public async Task TheToolsFreeLicenceIsAskedForAndTheGamesIsNot()
    {
        _session.IsLoggedIn = true;
        _content.NeedsLicense.Add(SkyrimSECreationKit);
        _content.GrantsFreeLicense.Add(SkyrimSECreationKit);
        _content.Publishes(SkyrimSECreationKit).Add(new DepotManifestId(1946182, 500));
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("CreationKit.exe"),
            "c:\\out\\CreationKit.exe".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(new[] {SkyrimSECreationKit}, _content.FreeLicensesAsked);
    }

    /// <summary>
    ///     The assertion this whole thing rests on. A Skyrim Special Edition list missing only
    ///     <c>Dawnguard.esm</c> gets it out of a base-game depot, and the Creation Kit is never reached -
    ///     so nothing is added to the user's Steam library by a repair that had nothing to do with it.
    /// </summary>
    [Fact]
    public async Task ARepairTheGamesOwnDepotsSatisfyNeverTouchesTheTool()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(2, 200));
        _content.Manifests[(2u, 200ul)] = new[] {"Data\\Dawnguard.esm"};
        _content.NeedsLicense.Add(SkyrimSECreationKit);
        _content.GrantsFreeLicense.Add(SkyrimSECreationKit);
        _content.Publishes(SkyrimSECreationKit).Add(new DepotManifestId(1946182, 500));
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Dawnguard.esm"),
            "c:\\out\\Dawnguard.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Empty(_content.FreeLicensesAsked);
        Assert.Empty(_content.GrantsRequested);
        Assert.Equal((SkyrimSE, 2u, 200ul), Assert.Single(_content.Searched));
    }

    /// <summary>
    ///     And the other half of it: a licence the account already holds is never asked for again. Skyrim's
    ///     Kit is the real case - app 202480 is granted by the packages that grant the game - and the cost
    ///     of getting this wrong is a package appearing in somebody's library for no reason.
    /// </summary>
    [Fact]
    public async Task ALicenceTheAccountAlreadyHoldsIsNotRequested()
    {
        _session.IsLoggedIn = true;
        _content.Licensed.Add(SkyrimSECreationKit);
        _content.Publishes(SkyrimSECreationKit).Add(new DepotManifestId(1946182, 500));
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("CreationKit.exe"),
            "c:\\out\\CreationKit.exe".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(new[] {SkyrimSECreationKit}, _content.FreeLicensesAsked);
        Assert.Empty(_content.GrantsRequested);
    }

    /// <summary>
    ///     An app Steam will not open says nothing about the game's own depots, which hold everything
    ///     except the tool's own files, so the search carries on rather than failing there.
    /// </summary>
    [Fact]
    public async Task AToolAppThatCannotBeReachedDoesNotEndTheSearch()
    {
        _session.IsLoggedIn = true;
        _content.NeedsLicense.Add(SkyrimSECreationKit);
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("Data/Skyrim.esm"),
            "c:\\out\\Skyrim.esm".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal((SkyrimSE, 1u, 100ul, "Data\\Skyrim.esm"), Assert.Single(_content.Downloaded));
    }

    /// <summary>
    ///     With the tool unreachable and the file in none of the game's depots, "no manifest has that file"
    ///     would be a claim about a manifest nobody managed to read. The refusal is what to report.
    /// </summary>
    [Fact]
    public async Task AToolThatRefusedIsReportedRatherThanCallingTheFileMissing()
    {
        _session.IsLoggedIn = true;
        _content.NeedsLicense.Add(SkyrimSECreationKit);
        _content.Current.Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("CreationKit.exe"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("1946180", result.Detail);
        Assert.Empty(_content.Downloaded);
    }

    /// <summary>
    ///     When both refused, the game's answer is the one to report. An account that does not hold Skyrim
    ///     Special Edition needs to hear about app 489830; "cannot get app token for 1946180" would send
    ///     them after a free tool that was never the problem.
    /// </summary>
    [Fact]
    public async Task TheGamesOwnRefusalOutranksATools()
    {
        _session.IsLoggedIn = true;
        _content.Current.Add(new DepotManifestId(489831, 100));
        _content.Refuses.Add(489831);
        _content.NeedsLicense.Add(SkyrimSECreationKit);

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, null, File("CreationKit.exe"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Contains("489831", result.Detail);
        Assert.DoesNotContain("app token", result.Detail);
    }

    /// <summary>
    ///     The version index records depot and manifest ids with no app beside them, so an id out of it can
    ///     only be asked for under the game's own app. Nothing about a tool is touched, licence included.
    /// </summary>
    [Fact]
    public async Task AVersionLookupNeverReachesTheTools()
    {
        _session.IsLoggedIn = true;
        _index.Versions[Version] = new[] {new SteamManifest {Depot = 1, Manifest = 100}};
        _content.Manifests[(1u, 100ul)] = new[] {"Data\\Skyrim.esm"};
        _content.Publishes(SkyrimSECreationKit).Add(new DepotManifestId(1946182, 500));
        _content.Manifests[(1946182u, 500ul)] = new[] {"CreationKit.exe"};

        var result = await Restorer().Restore(Game.SkyrimSpecialEdition, Version, File("CreationKit.exe"),
            default, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Equal(new[] {(SkyrimSE, 1u, 100ul)}, _content.Searched);
        Assert.Empty(_content.FreeLicensesAsked);
        Assert.Single(_index.Asked);
    }

    /// <summary>
    ///     A game with no companion app behaves exactly as it did: one app, searched once.
    /// </summary>
    [Fact]
    public async Task AGameWithNoToolsAsksAboutNothingElse()
    {
        _session.IsLoggedIn = true;
        Assert.Empty(Game.Fallout3.MetaData().SteamToolIDs);
        _content.Publishes(22300).Add(new DepotManifestId(1, 100));
        _content.Manifests[(1u, 100ul)] = new[] {"Fallout3.exe"};

        var result = await Restorer().Restore(Game.Fallout3, null, File("Fallout3.exe"),
            "c:\\out\\Fallout3.exe".ToAbsolutePath(), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Empty(_content.FreeLicensesAsked);
    }
}
