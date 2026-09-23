using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Translation.Nexus;

public sealed record TranslationFile(
    Game Game,
    long ModId,
    long FileId,
    string ModName,
    string FileName,
    string DisplayName,
    string Category,
    long Size,
    DateTime Uploaded,
    IReadOnlyList<string> Plugins)
{
    public string Url => $"https://www.nexusmods.com/{Game.MetaData().NexusDomain}/mods/{ModId}?tab=files&file_id={FileId}";
}

public sealed class NexusTranslationFinder
{
    private static readonly string GraphQlUrl =
        Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL") ?? "https://api.nexusmods.com/v2/graphql";

    private const long MaxFileSize = 1L << 30;
    private const int FilesPerPlugin = 2;

    private readonly HttpClient _client;
    private readonly NexusApi _nexus;
    private readonly ILogger<NexusTranslationFinder> _logger;

    public NexusTranslationFinder(HttpClient client, NexusApi nexus, ILogger<NexusTranslationFinder> logger)
    {
        _client = client;
        _nexus = nexus;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TranslationFile>> Find(Game game, IReadOnlyCollection<string> plugins,
        GameLanguage language, Action<string, double>? progress, CancellationToken token)
    {
        var meta = game.MetaData();
        var hits = new List<(string Plugin, long ModId, long FileId)>();
        var done = 0;
        using var gate = new SemaphoreSlim(6);
        var lookups = plugins.Select(async plugin =>
        {
            await gate.WaitAsync(token);
            try
            {
                var found = await FilesContaining(meta.NexusGameId, plugin, token);
                lock (hits) hits.AddRange(found.Select(f => (plugin, f.ModId, f.FileId)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Nexus file search for {Plugin} failed: {Message}", plugin, ex.Message);
            }
            finally
            {
                gate.Release();
                var checkedCount = Interlocked.Increment(ref done);
                progress?.Invoke($"Checked {checkedCount} of {plugins.Count} plugins", (double) checkedCount / plugins.Count);
            }
        });
        await Task.WhenAll(lookups);

        var modIds = hits.Select(h => h.ModId).Distinct().ToList();
        _logger.LogInformation("Found {Files} files in {Mods} mods that contain the modlist's plugins",
            hits.Count, modIds.Count);
        var languages = await Languages(meta.NexusGameId, modIds, progress, token);
        var wanted = hits.Where(h => languages.TryGetValue(h.ModId, out var l) &&
                                     language.NexusLanguageNames.Contains(l.Language, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var wantedMods = wanted.Select(w => w.ModId).Distinct().ToList();
        _logger.LogInformation("{Mods} of those mods are {Language} translations", wantedMods.Count, language.DisplayName);
        var details = new Dictionary<(long, long), (string ModName, Networking.NexusApi.DTOs.ModFile File)>();
        var listed = 0;
        progress?.Invoke($"Reading the file lists of {wantedMods.Count} {language.DisplayName} mods", 0);
        await Parallel.ForEachAsync(wantedMods, new ParallelOptions {MaxDegreeOfParallelism = 4, CancellationToken = token},
            async (modId, ct) =>
            {
                try
                {
                    var (files, _) = await _nexus.ModFiles(meta.NexusDomain, modId, ct);
                    lock (details)
                        foreach (var file in files.Files)
                            details[(modId, file.FileId)] = (languages[modId].Name, file);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Could not list files of Nexus mod {ModId}: {Message}", modId, ex.Message);
                }

                var count = Interlocked.Increment(ref listed);
                progress?.Invoke($"Read the file lists of {count} of {wantedMods.Count} {language.DisplayName} mods",
                    (double) count / wantedMods.Count);
            });

        var chosen = new Dictionary<(long, long), HashSet<string>>();
        foreach (var byPlugin in wanted.GroupBy(w => w.Plugin, StringComparer.OrdinalIgnoreCase))
        {
            var ranked = byPlugin
                .Select(w => (w.ModId, w.FileId, Found: details.TryGetValue((w.ModId, w.FileId), out var d), Detail: d))
                .Where(x => x.Found && Size(x.Detail.File) is > 0 and <= MaxFileSize)
                .GroupBy(x => x.ModId)
                .Select(g => g.OrderBy(x => CategoryRank(x.Detail.File.CategoryName))
                    .ThenByDescending(x => x.Detail.File.UploadedTimestamp).First())
                .OrderBy(x => CategoryRank(x.Detail.File.CategoryName))
                .ThenByDescending(x => x.Detail.File.UploadedTimestamp)
                .Take(FilesPerPlugin);
            foreach (var pick in ranked)
            {
                if (!chosen.TryGetValue((pick.ModId, pick.FileId), out var set))
                    chosen[(pick.ModId, pick.FileId)] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(byPlugin.Key);
            }
        }

        return chosen.Select(kv =>
        {
            var (modName, file) = details[kv.Key];
            return new TranslationFile(game, kv.Key.Item1, kv.Key.Item2, modName, file.FileName, file.Name,
                file.CategoryName ?? "", Size(file), DateTimeOffset.FromUnixTimeSeconds(file.UploadedTimestamp).UtcDateTime,
                kv.Value.OrderBy(p => p).ToList());
        }).OrderBy(f => f.ModName).ToList();
    }

    private static long Size(Networking.NexusApi.DTOs.ModFile file) => file.SizeInBytes ?? file.SizeKb * 1024L;

    private static int CategoryRank(string? category) => category?.ToUpperInvariant() switch
    {
        "MAIN" => 0,
        "UPDATE" => 1,
        "OPTIONAL" => 2,
        "MISCELLANEOUS" => 3,
        "ARCHIVED" => 5,
        "OLD_VERSION" => 6,
        _ => 4
    };

    private async Task<List<(long ModId, long FileId)>> FilesContaining(long gameId, string plugin,
        CancellationToken token)
    {
        const string query = """
            query($game:Int!,$name:String!,$offset:Int!){ modFileContents(filter:{ gameId:[{value:$game,op:EQUALS}], fileNameWildcard:[{value:$name,op:WILDCARD}] }, count:100, offset:$offset){ totalCount nodes { modId fileId filePath } } }
            """;
        var result = new List<(long, long)>();
        // Wildcard characters in a plugin name would widen the search
        var pattern = new string(plugin.Select(c => c is '*' or '?' or '[' or ']' ? '?' : c).ToArray());
        for (var offset = 0; offset < 1000; offset += 100)
        {
            var data = await Query(query, new {game = gameId, name = pattern, offset}, token);
            var page = data["modFileContents"]!;
            var nodes = page["nodes"]!.AsArray();
            foreach (var node in nodes)
            {
                var path = node!["filePath"]!.GetValue<string>().Replace('\\', '/');
                if (path.Split('/')[^1].Equals(plugin, StringComparison.OrdinalIgnoreCase))
                    result.Add((long.Parse(node["modId"]!.ToString()), long.Parse(node["fileId"]!.ToString())));
            }

            if (nodes.Count == 0 || offset + 100 >= page["totalCount"]!.GetValue<int>()) break;
        }

        return result.Distinct().ToList();
    }

    private async Task<Dictionary<long, (string Language, string Name)>> Languages(long gameId,
        IReadOnlyList<long> modIds, Action<string, double>? progress, CancellationToken token)
    {
        var result = new Dictionary<long, (string, string)>();
        var looked = 0;
         // One aliased query per 40 mods
        foreach (var chunk in modIds.Chunk(40))
        {
            progress?.Invoke($"Checking the language of {modIds.Count} mods ({looked} done)",
                (double) looked / Math.Max(1, modIds.Count));
            looked += chunk.Length;
            var parts = chunk.Select(id =>
                $"m{id}: mods(filter:{{ gameId:[{{value:\"{gameId}\",op:EQUALS}}], modId:[{{value:\"{id}\",op:EQUALS}}] }}, facets:{{ languageName:[] }}, count:1){{ facetsData nodes {{ modId name }} }}");
            JsonNode data;
            try
            {
                data = await Query("{ " + string.Join(" ", parts) + " }", null, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Nexus language lookup failed: {Message}", ex.Message);
                continue;
            }

            foreach (var id in chunk)
            {
                var entry = data[$"m{id}"];
                var nodes = entry?["nodes"]?.AsArray();
                if (nodes == null || nodes.Count == 0) continue;
                var languages = entry!["facetsData"]?["languageName"]?.AsObject();
                var language = languages?.FirstOrDefault(l => l.Value?.GetValue<int>() > 0).Key;
                if (language != null)
                    result[id] = (language, nodes[0]!["name"]!.GetValue<string>());
            }
        }

        return result;
    }

    private async Task<JsonNode> Query(string query, object? variables, CancellationToken token)
    {
        var body = JsonSerializer.Serialize(new {query, variables});
        for (var attempt = 0;; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Wabbajack", "1.0"));
            request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
            try
            {
                using var response = await _client.SendAsync(request, token);
                response.EnsureSuccessStatusCode();
                var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                if (root["errors"] is JsonArray {Count: > 0} errors)
                    throw new InvalidOperationException(errors[0]?["message"]?.ToString() ?? "GraphQL error");
                return root["data"]!;
            }
            catch (HttpRequestException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 + attempt * 3), token);
            }
        }
    }
}
