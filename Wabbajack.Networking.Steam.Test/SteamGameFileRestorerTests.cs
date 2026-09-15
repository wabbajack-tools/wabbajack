using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
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

    [Fact]
    public void WithNoStoredLoginItSaysWhatOneWouldBuy()
    {
        var status = Restorer().Status();

        Assert.False(status.Ready);
        Assert.Contains("Log into Steam", status.Reason);
        Assert.Contains("never written to", status.Reason);
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

    private sealed class FakeIndex : ISteamManifestIndex
    {
        public Dictionary<string, SteamManifest[]> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(Game Game, string Version)> Asked { get; } = new();

        public Task<SteamManifest[]> Get(Game game, string version, CancellationToken token)
        {
            Asked.Add((game, version));
            return Task.FromResult(Versions.TryGetValue(version, out var manifests)
                ? manifests
                : Array.Empty<SteamManifest>());
        }
    }

    /// <summary>
    ///     A depot that is a list of file names. <see cref="DepotPaths.Find" /> does the matching for real,
    ///     since that is the part a wrong answer would put the wrong bytes on somebody's disk.
    /// </summary>
    private sealed class FakeContentClient : ISteamContentClient
    {
        public List<DepotManifestId> Current { get; } = new();
        public Dictionary<(uint Depot, ulong Manifest), string[]> Manifests { get; } = new();

        /// <summary>Depots the account has no licence for.</summary>
        public HashSet<uint> Refuses { get; } = new();

        public List<(uint App, uint Depot, ulong Manifest)> Searched { get; } = new();
        public List<(uint App, uint Depot, ulong Manifest, string Path)> Downloaded { get; } = new();

        public Task<AppInfo> GetAppInfo(uint appId)
        {
            return Task.FromResult(new AppInfo());
        }

        public Task<ulong?> GetCurrentManifestIdAsync(uint appId, uint depotId,
            string branch = SteamContentClient.PublicBranch)
        {
            return Task.FromResult(Current.FirstOrDefault(c => c.DepotId == depotId)?.ManifestId);
        }

        public Task<IReadOnlyList<DepotManifestId>> GetCurrentDepotsAsync(uint appId,
            string branch = SteamContentClient.PublicBranch)
        {
            return Task.FromResult<IReadOnlyList<DepotManifestId>>(Current.ToArray());
        }

        public Task<DepotAccess> CheckAccessAsync(uint appId, uint depotId, CancellationToken token)
        {
            return Task.FromResult(Refuses.Contains(depotId) ? DepotAccess.NotEntitled : DepotAccess.Granted);
        }

        public Task EnsureAccessAsync(uint appId, uint depotId, CancellationToken token)
        {
            if (Refuses.Contains(depotId))
                throw new SteamNoEntitlementException(
                    $"The Steam account holds no licence for depot {depotId} of app {appId}", appId, depotId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DepotFile>> ListFilesAsync(uint appId, uint depotId, ulong manifestId,
            CancellationToken token, string branch = SteamContentClient.PublicBranch)
        {
            return Task.FromResult<IReadOnlyList<DepotFile>>(Files(depotId, manifestId));
        }

        public async Task<DepotFile?> FindFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
            CancellationToken token, string branch = SteamContentClient.PublicBranch)
        {
            await EnsureAccessAsync(appId, depotId, token);
            Searched.Add((appId, depotId, manifestId));
            return Match(depotId, manifestId, depotPath);
        }

        public async Task<DepotFile> DownloadFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
            AbsolutePath output, CancellationToken token, IJob? parentJob = null,
            string branch = SteamContentClient.PublicBranch)
        {
            await EnsureAccessAsync(appId, depotId, token);
            var found = Match(depotId, manifestId, depotPath)
                        ?? throw new SteamFileNotInDepotException($"No '{depotPath}' in {depotId}", depotPath);

            Downloaded.Add((appId, depotId, manifestId, found.Path));
            return found;
        }

        /// <summary>The real matching rule, since a wrong answer here is wrong bytes on somebody's disk.</summary>
        private DepotFile? Match(uint depotId, ulong manifestId, string depotPath)
        {
            var names = Manifests.TryGetValue((depotId, manifestId), out var listed)
                ? listed
                : Array.Empty<string>();

            var match = names.FirstOrDefault(n => DepotPaths.AreSame(n, depotPath)) ??
                        names.SingleOrDefault(n => DepotPaths.EndsWithPath(n, depotPath));

            return match == null ? null : new DepotFile(match, 1, string.Empty);
        }

        private DepotFile[] Files(uint depotId, ulong manifestId)
        {
            return Manifests.TryGetValue((depotId, manifestId), out var names)
                ? names.Select(n => new DepotFile(n, 1, string.Empty)).ToArray()
                : Array.Empty<DepotFile>();
        }
    }

    private sealed class FakeSession : ISteamSession
    {
        public bool LoggedInWithStoredToken { get; private set; }
        public bool IsLoggedIn { get; set; }
        public string? AccountName => "tester";
        public bool HaveStoredToken { get; set; }

        public Task<SteamLoginResult> LoginWithStoredTokenAsync(CancellationToken token)
        {
            if (!HaveStoredToken) throw new SteamLoginRequiredException("There is no saved Steam login.");
            LoggedInWithStoredToken = true;
            IsLoggedIn = true;
            return Task.FromResult(new SteamLoginResult("tester", 1, true, null));
        }

        public Task<SteamLoginResult> LoginWithQrCodeAsync(Action<string> onChallengeUrl, CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public Task<SteamLoginResult> LoginWithCredentialsAsync(string username, string password,
            CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public ValueTask<SteamLogoutResult> LogoutAsync()
        {
            return ValueTask.FromResult(SteamLogoutResult.NothingStored);
        }

        public void Dispose()
        {
        }
    }
}
