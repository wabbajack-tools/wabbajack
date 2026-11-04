using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     Publishes collections to <see cref="FakeNexus" /> through the real uploader, start to finish: multipart
///     session, parts to presigned storage, completion, finalise, waiting for the upload, and the collection or
///     revision call, with the GraphQL lookups around it. Every request has to satisfy the contract snapshots,
///     so these fail when the uploader sends something Nexus' published API does not describe.
/// </summary>
public class NexusCollectionUploaderTests : IAsyncLifetime
{
    private const string ListName = "Contract Test List";

    private readonly ILogger<NexusCollectionUploaderTests> _logger;
    private readonly TemporaryFileManager _manager;
    private readonly FakeNexus _nexus = new();
    private TemporaryPath _folder;
    private AbsolutePath _archive;
    private byte[] _archiveBytes = Array.Empty<byte>();

    public NexusCollectionUploaderTests(ILogger<NexusCollectionUploaderTests> logger, TemporaryFileManager manager)
    {
        _logger = logger;
        _manager = manager;
    }

    public async Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        _archive = _folder.Path.Combine("Contract Test List.wabbajack");
        // Three and a bit of the fake's 64 KB parts, so the last part is short as it usually is.
        _archiveBytes = Enumerable.Range(0, 200_000).Select(i => (byte) (i * 7 % 251)).ToArray();
        await _archive.WriteAllBytesAsync(_archiveBytes);
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    private NexusCollectionUploader Uploader(bool expiredLogin = false)
    {
        return new NexusCollectionUploader(_logger, FakeNexus.Login(expired: expiredLogin), _nexus.Client(),
            new JsonSerializerOptions())
        {
            UploadClientFactory = _nexus.Client,
            Delay = (_, _) => Task.CompletedTask
        };
    }

    private static ModList ModList()
    {
        return new ModList
        {
            Name = ListName,
            Author = "Tester",
            Description = "A list the contract tests publish.",
            Website = "https://example.com/list",
            GameType = Game.SkyrimSpecialEdition,
            Archives = new[]
            {
                new Archive
                {
                    Name = "SkyUI.7z", Size = 1_000_000,
                    State = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 100, FileID = 200, Name = "SkyUI", Version = "5.2", Author = "SkyUI Team"}
                },
                new Archive
                {
                    Name = "USSEP.7z", Size = 2_000_000,
                    State = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 101, FileID = 201, Name = "USSEP", Version = "4.3"}
                },
                new Archive
                {
                    Name = "Tool.zip", Size = 3000,
                    State = new Http {Url = new Uri("https://example.com/tool.zip")}
                },
                new Archive
                {
                    Name = "Skyrim.esm", Size = 4000,
                    State = new GameFileSource {Game = Game.SkyrimSpecialEdition, GameFile = "Data/Skyrim.esm".ToRelativePath()}
                }
            }
        };
    }

    private Task<CollectionUploadResult?> Publish(string? collectionId = null, string? slug = null, NexusCollectionUploader? uploader = null)
    {
        return (uploader ?? Uploader()).UploadCollection(ModList(), _folder.Path.Combine("collection.json"), _archive,
            collectionId, slug, "1.6.1170");
    }

    private void AssertNoViolations()
    {
        Assert.True(_nexus.Violations.IsEmpty,
            "Requests outside the Nexus contract:\n  " + string.Join("\n  ", _nexus.Violations));
    }

    private static JsonArray Mods(FakeRevision revision)
    {
        return (JsonArray) revision.Payload!["collection_manifest"]!["mods"]!;
    }

    [Fact]
    public async Task NewCollection_IsPublishedWithItsArchiveIntact()
    {
        var result = await Publish();

        AssertNoViolations();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        var collection = Assert.Single(_nexus.Collections);
        Assert.Equal(collection.Id, result.CollectionId);
        Assert.Equal(collection.Slug, result.Slug);
        Assert.Equal(1, result.RevisionNumber);

        var revision = Assert.Single(collection.Revisions);
        Assert.Equal(_archiveBytes, revision.Upload!.Assembled);
        Assert.Equal(4, revision.Upload.Parts.Count);
        Assert.Equal(ListName, collection.Name);
        Assert.Equal(FakeNexus.Domain, collection.DomainName);
        Assert.Equal(new[] {"100", "101", ""}, Mods(revision).Select(m => m!["source"]!["mod_id"]!.GetValue<string>()));
        Assert.Equal(new[] {"nexus", "nexus", "browse"}, Mods(revision).Select(m => m!["source"]!["type"]!.GetValue<string>()));
        Assert.Equal("1.6.1170", revision.Payload!["collection_manifest"]!["info"]!["game_versions"]![0]!.GetValue<string>());
    }

    /// <summary>
    ///     A revision's payload carries the summary and description already on the collection's page, read back
    ///     over GraphQL, so publishing an update does not overwrite text the author edited on the site.
    /// </summary>
    [Fact]
    public async Task Revision_KeepsTheTextOnTheCollectionPage()
    {
        var existing = _nexus.AddCollection(ListName, revisions: 2, summary: "Edited on the site",
            description: "Long description edited on the site");

        var result = await Publish(existing.Id, existing.Slug);

        AssertNoViolations();
        Assert.Equal(existing.Id, result!.CollectionId);
        Assert.Equal(existing.Slug, result.Slug);
        Assert.Equal(3, result.RevisionNumber);
        var info = existing.Revisions[^1].Payload!["collection_manifest"]!["info"]!;
        Assert.Equal("Edited on the site", info["summary"]!.GetValue<string>());
        Assert.Equal("Long description edited on the site", info["description"]!.GetValue<string>());
    }

    /// <summary>
    ///     A revision's answer has no slug in it. Without one stored, the uploader finds the collection among
    ///     the user's own, by the id the answer did carry.
    /// </summary>
    [Fact]
    public async Task Revision_WithNoStoredSlug_FindsItAmongTheUsersCollections()
    {
        _nexus.AddCollection(ListName, revisions: 1);
        var existing = _nexus.AddCollection(ListName, revisions: 1);
        _nexus.AddCollection("Something else", revisions: 1);

        var result = await Publish(existing.Id, slug: null);

        AssertNoViolations();
        Assert.Equal(existing.Slug, result!.Slug);
        Assert.Equal(2, result.RevisionNumber);
    }

    [Fact]
    public async Task Revision_OfACollectionNexusNoLongerHas_PublishesANewOne()
    {
        var result = await Publish("424242", "deleted-collection");

        AssertNoViolations();
        var collection = Assert.Single(_nexus.Collections);
        Assert.Equal(collection.Id, result!.CollectionId);
        Assert.Equal(1, result.RevisionNumber);
    }

    /// <summary>A rejection that is not a missing collection must never be answered by making a second one.</summary>
    [Fact]
    public async Task Revision_NexusRejects_DoesNotCreateADuplicate()
    {
        var existing = _nexus.AddCollection(ListName, revisions: 1);
        _nexus.RejectCollection = _ => new JsonArray(new JsonObject
        {
            ["detail"] = "Adult content flag cannot change on a revision", ["pointer"] = "/collection_data/adult_content"
        });

        var result = await Publish(existing.Id, existing.Slug);

        AssertNoViolations();
        Assert.Null(result);
        Assert.Single(_nexus.Collections);
        Assert.Single(existing.Revisions);
    }

    /// <summary>
    ///     Nexus answers a manifest naming a mod or file it does not have with a 422 per entry. Those are dropped
    ///     and the rest published, rather than the whole collection failing over one removed mod.
    /// </summary>
    [Fact]
    public async Task ModsNexusDoesNotHave_AreDroppedAndTheRestPublished()
    {
        _nexus.RejectCollection = data =>
        {
            var mods = (JsonArray) data["collection_manifest"]!["mods"]!;
            var bad = mods.Select((m, i) => (m, i))
                .Where(x => x.m!["source"]!["mod_id"]!.GetValue<string>() == "101")
                .Select(x => (JsonNode) new JsonObject
                {
                    ["detail"] = $"Mod 101, {FakeNexus.Domain} not available",
                    ["pointer"] = $"/collection_data/collection_manifest/mods/{x.i}/source/mod_id"
                }).ToArray();
            return bad.Length == 0 ? null : new JsonArray(bad);
        };

        var result = await Publish();

        AssertNoViolations();
        Assert.True(result!.Success);
        var revision = Assert.Single(Assert.Single(_nexus.Collections).Revisions);
        Assert.Equal(new[] {"100", ""}, Mods(revision).Select(m => m!["source"]!["mod_id"]!.GetValue<string>()));
    }

    [Fact]
    public async Task SummaryNexusRejects_IsLeftOutAndTheCollectionPublished()
    {
        _nexus.RejectCollection = data =>
            data["collection_manifest"]!["info"]!["summary"] == null
                ? null
                : new JsonArray(new JsonObject
                {
                    ["detail"] = "Summary contains a link", ["pointer"] = "/collection_data/collection_manifest/info/summary"
                });

        var result = await Publish();

        AssertNoViolations();
        Assert.True(result!.Success);
        var info = Assert.Single(Assert.Single(_nexus.Collections).Revisions).Payload!["collection_manifest"]!["info"]!;
        Assert.Null(info["summary"]);
        Assert.NotNull(info["description"]);
    }

    [Fact]
    public async Task ExpiredLogin_SendsNothing()
    {
        var result = await Publish(uploader: Uploader(expiredLogin: true));

        Assert.Null(result);
        Assert.Empty(_nexus.Requests);
    }
}
