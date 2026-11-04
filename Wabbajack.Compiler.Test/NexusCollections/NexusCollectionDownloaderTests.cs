using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     Resolves a <c>wabbajack://nexus-collection/...</c> link against <see cref="FakeNexus" /> through the real
///     downloader: the GraphQL revision lookup, then the download link it names, then the file itself.
/// </summary>
public class NexusCollectionDownloaderTests
{
    private static readonly byte[] Archive = "a collection archive"u8.ToArray();

    private readonly ILogger<NexusCollectionDownloader> _logger;
    private readonly FakeNexus _nexus = new();

    public NexusCollectionDownloaderTests(ILogger<NexusCollectionDownloader> logger)
    {
        _logger = logger;
    }

    private NexusCollectionDownloader Downloader(bool gameInstalled = true, bool expiredLogin = false)
    {
        return new NexusCollectionDownloader(_logger, FakeNexus.Login(expired: expiredLogin), _nexus.Client(),
            new Games(gameInstalled));
    }

    private FakeCollection Published(int revisions)
    {
        var collection = _nexus.AddCollection("Published List", revisions);
        foreach (var revision in collection.Revisions)
            _nexus.RevisionArchives[(collection.Slug, revision.Number)] = Archive;
        return collection;
    }

    private void AssertNoViolations()
    {
        Assert.True(_nexus.Violations.IsEmpty,
            "Requests outside the Nexus contract:\n  " + string.Join("\n  ", _nexus.Violations));
    }

    [Fact]
    public async Task LatestRevision_ResolvesToAFileThatDownloads()
    {
        var collection = Published(revisions: 3);

        var info = await Downloader().GetCollectionDownloadInfo(collection.Slug, revisionNumber: null);

        AssertNoViolations();
        Assert.NotNull(info);
        Assert.Equal(3, info!.RevisionNumber);
        Assert.Equal(int.Parse(collection.Id), info.CollectionId);
        Assert.Equal(int.Parse(collection.Revisions[2].Id), info.RevisionId);
        Assert.Equal("Published List", info.CollectionName);
        Assert.Equal(Game.SkyrimSpecialEdition, info.RequiredGame);
        Assert.Equal(Archive, await _nexus.Client().GetByteArrayAsync(info.DownloadUrl));
    }

    [Fact]
    public async Task SpecificRevision_IsTheOneResolved()
    {
        var collection = Published(revisions: 3);

        var info = await Downloader().GetCollectionDownloadInfo(collection.Slug, revisionNumber: 2);

        AssertNoViolations();
        Assert.Equal(2, info!.RevisionNumber);
        Assert.Equal(int.Parse(collection.Revisions[1].Id), info.RevisionId);
    }

    [Fact]
    public async Task UnknownCollection_ResolvesToNothing()
    {
        var info = await Downloader().GetCollectionDownloadInfo("no-such-collection", null);

        AssertNoViolations();
        Assert.Null(info);
    }

    [Fact]
    public async Task GameNotInstalled_IsReportedByName()
    {
        var collection = Published(revisions: 1);
        var downloader = Downloader(gameInstalled: false);

        var info = await downloader.GetCollectionDownloadInfo(collection.Slug, null);

        Assert.Null(info);
        Assert.Contains("is not installed", downloader.LastError);
    }

    [Fact]
    public async Task ExpiredLogin_AsksTheUserToLogIn()
    {
        var downloader = Downloader(expiredLogin: true);

        var info = await downloader.GetCollectionDownloadInfo("anything", null);

        Assert.Null(info);
        Assert.Contains("log in", downloader.LastError);
        Assert.Empty(_nexus.Requests);
    }

    private sealed class Games(bool installed) : IGameLocator
    {
        public AbsolutePath GameLocation(Game game) => default;
        public bool IsInstalled(Game game) => installed;
        public bool TryFindLocation(Game game, out AbsolutePath path)
        {
            path = default;
            return installed;
        }

        public bool TryGetSteamBuildId(Game game, out string buildId)
        {
            buildId = "";
            return false;
        }

        public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests)
        {
            manifests = System.Array.Empty<SteamManifest>();
            return false;
        }
    }
}
