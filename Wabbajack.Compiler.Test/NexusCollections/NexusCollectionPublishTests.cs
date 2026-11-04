using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     Runs only when <c>WABBAJACK_NEXUS_PUBLISH=1</c>: these publish to the real Nexus Mods, under the account
///     logged in on this machine, and so are never part of an ordinary test run, a full one included.
/// </summary>
public sealed class NexusPublishFactAttribute : FactAttribute
{
    public const string Variable = "WABBAJACK_NEXUS_PUBLISH";

    public NexusPublishFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"Publishes a collection revision to Nexus Mods; set {Variable}=1 to run it.";
    }
}

/// <summary>
///     Publishes a real modlist to Nexus Mods as a collection and installs it back, the whole way round, against
///     the live service. The fake in <see cref="NexusCollectionUploaderTests" /> checks the code against what Nexus
///     documents; this checks that what Nexus documents is what it does.
///     <para>
///         The list is Halgari's Helper, downloaded from the gallery (or a local <c>.wabbajack</c> named by
///         <c>WABBAJACK_NEXUS_PUBLISH_LIST</c>). It goes up as a new draft revision of one long-lived collection,
///         whose id is kept in <c>nexus-publish-test.json</c> in the Wabbajack app data folder; the first run
///         creates it. Nothing is ever published, and Nexus updates a collection's draft revision in place rather
///         than adding another, so every run replaces the same draft; the test holds Nexus to that, and discards
///         any revision a run does add.
///     </para>
/// </summary>
public class NexusCollectionPublishTests
{
    private const string ListMachineUrl = "halgari/halgaris_helper";
    private const string ListVariable = "WABBAJACK_NEXUS_PUBLISH_LIST";

    private static readonly AbsolutePath StateFile =
        KnownFolders.WabbajackAppLocal.Combine("nexus-publish-test.json");

    private readonly ITestOutputHelper _output;
    private readonly TemporaryFileManager _manager;

    public NexusCollectionPublishTests(ITestOutputHelper output, TemporaryFileManager manager)
    {
        _output = output;
        _manager = manager;
    }

    [NexusPublishFact]
    [Trait("Category", "RequiresOAuth")]
    [Trait("Category", "RequiresNetwork")]
    public async Task HalgarisHelper_PublishesAsADraftRevisionAndInstallsBack()
    {
        var services = new ServiceCollection().AddLogging(l => l.AddProvider(new OutputLogger(_output)));
        services.AddSingleton<IUserInterventionHandler, CancelInterventions>();
        services.AddOSIntegrated();
        await using var live = services.BuildServiceProvider();

        // Refreshes an expired Nexus login and stores the new one, the way the app does on its first call.
        Assert.Equal(NexusCredentialSource.OAuth, await live.GetRequiredService<NexusApi>().CredentialSource());
        var login = live.GetRequiredService<ITokenProvider<NexusOAuthState>>();
        var accessToken = (await login.Get())!.OAuth!.AccessToken!;

        await using var folder = _manager.CreateFolder();
        var wabbajack = await TheList(live, folder.Path);
        var modList = await StandardInstaller.LoadFromFile(live.GetRequiredService<DTOSerializer>(), wabbajack);
        var known = await LoadState();
        _output.WriteLine(known == null ? "No test collection yet; this run creates it." : $"Test collection {known.CollectionId} ({known.Slug})");

        var uploader = new NexusCollectionUploader(live.GetRequiredService<ILogger<NexusCollectionPublishTests>>(),
            login, live.GetRequiredService<HttpClient>(), live.GetRequiredService<DTOSerializer>().Options);
        var result = await uploader.UploadCollection(modList, folder.Path.Combine("collection.json"), wabbajack,
            known?.CollectionId, known?.Slug);

        Assert.True(result is {Success: true}, "Publishing to Nexus Mods failed; the log above has Nexus' answer.");
        if (known == null)
            await SaveState(new TestCollection(result!.CollectionId, result.Slug, result.RevisionNumber));
        _output.WriteLine($"Published {result.Slug} revision {result.RevisionNumber}");

        try
        {
            using var client = new HttpClient();

            var revision = await GraphQL(client, accessToken, @"
                query revision($slug: String!, $revision: Int) {
                  collectionRevision(slug: $slug, revision: $revision, viewAdultContent: true) {
                    revisionNumber
                    revisionStatus
                  }
                }", new JsonObject {["slug"] = result.Slug, ["revision"] = result.RevisionNumber});
            Assert.Equal(result.RevisionNumber, revision["collectionRevision"]!["revisionNumber"]!.GetValue<int>());
            Assert.Equal("draft", revision["collectionRevision"]!["revisionStatus"]!.GetValue<string>());

            var downloader = new NexusCollectionDownloader(live.GetRequiredService<ILogger<NexusCollectionDownloader>>(),
                login, live.GetRequiredService<HttpClient>(), new EveryGameInstalled());
            var info = await downloader.GetCollectionDownloadInfo(result.Slug, result.RevisionNumber);
            Assert.True(info != null, $"The published revision does not resolve for installing: {downloader.LastError}");
            Assert.Equal(Game.SkyrimSpecialEdition, info!.RequiredGame);

            var downloaded = await client.GetByteArrayAsync(info.DownloadUrl);
            Assert.Equal(await (await wabbajack.ReadAllBytesAsync()).Hash(), await downloaded.Hash());

            // Uploading to a collection whose latest revision is a draft replaces that draft. If Nexus starts
            // adding a revision instead, every run would leave one behind; the finally below discards it, and
            // this says why the number moved.
            if (known != null)
                Assert.True(result.RevisionNumber == known.DraftRevision,
                    $"Nexus added revision {result.RevisionNumber} instead of replacing draft revision {known.DraftRevision}");
        }
        finally
        {
            // The draft the state file records keeps the collection in existence; any other is this run's alone.
            if (result.RevisionNumber != (known?.DraftRevision ?? result.RevisionNumber))
            {
                using var client = new HttpClient();
                var discarded = await GraphQL(client, accessToken, @"
                    mutation discard($collectionId: ID!, $revisionNumber: Int!) {
                      discardRevision(collectionId: $collectionId, revisionNumber: $revisionNumber, reason: ""Wabbajack publish test"") {
                        success
                      }
                    }", new JsonObject {["collectionId"] = result.CollectionId, ["revisionNumber"] = result.RevisionNumber});
                _output.WriteLine($"Discarded revision {result.RevisionNumber}: {discarded.ToJsonString()}");
            }
        }
    }

    /// <summary>The list to publish: a local file if one is named, otherwise Halgari's Helper from the gallery.</summary>
    private async Task<AbsolutePath> TheList(IServiceProvider live, AbsolutePath folder)
    {
        if (Environment.GetEnvironmentVariable(ListVariable) is { Length: > 0 } local)
            return local.ToAbsolutePath();

        var lists = await live.GetRequiredService<Client>().LoadLists();
        var list = lists.FirstOrDefault(l => l.NamespacedName == ListMachineUrl)
                   ?? throw new InvalidDataException($"{ListMachineUrl} is not in the gallery");
        var dispatcher = live.GetRequiredService<DownloadDispatcher>();
        var path = folder.Combine("halgaris_helper.wabbajack");
        await dispatcher.Download(new Archive
        {
            Name = path.FileName.ToString(),
            Hash = list.DownloadMetadata!.Hash,
            Size = list.DownloadMetadata.Size,
            State = dispatcher.Parse(new Uri(list.Links.Download))!
        }, path, CancellationToken.None);
        return path;
    }

    private static async Task<JsonNode> GraphQL(HttpClient client, string accessToken, string query, JsonObject variables)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NexusGraphQL.Endpoint)
        {
            Content = new StringContent(new JsonObject {["query"] = query, ["variables"] = variables}.ToJsonString(),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
        request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
        using var response = await client.SendAsync(request);
        var reply = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        if (reply["errors"] is JsonArray {Count: > 0} errors)
            throw new InvalidDataException($"Nexus GraphQL errors: {errors.ToJsonString()}");
        return reply["data"]!;
    }

    private record TestCollection(string CollectionId, string Slug, int DraftRevision);

    private static async Task<TestCollection?> LoadState()
    {
        if (!StateFile.FileExists()) return null;
        return JsonSerializer.Deserialize<TestCollection>(await StateFile.ReadAllTextAsync());
    }

    private static async Task SaveState(TestCollection state)
    {
        StateFile.Parent.CreateDirectory();
        await StateFile.WriteAllTextAsync(JsonSerializer.Serialize(state));
    }

    /// <summary>The test publishes from a list rather than an install, so whether the game is here does not matter.</summary>
    private sealed class EveryGameInstalled : IGameLocator
    {
        public AbsolutePath GameLocation(Game game) => default;
        public bool IsInstalled(Game game) => true;
        public bool TryFindLocation(Game game, out AbsolutePath path)
        {
            path = default;
            return true;
        }

        public bool TryGetSteamBuildId(Game game, out string buildId)
        {
            buildId = "";
            return false;
        }

        public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests)
        {
            manifests = Array.Empty<SteamManifest>();
            return false;
        }
    }

    private sealed class CancelInterventions : IUserInterventionHandler
    {
        public void Raise(IUserIntervention intervention) => intervention.Cancel();
    }

    private sealed class OutputLogger(ITestOutputHelper output) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                output.WriteLine($"{logLevel}: {formatter(state, exception)}{(exception == null ? "" : " " + exception.Message)}");
            }
            catch (InvalidOperationException)
            {
                // Output written after the test finished has nowhere to go.
            }
        }
    }
}
