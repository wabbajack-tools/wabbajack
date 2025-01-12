using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Octokit;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.Configs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.DTOs.Validation;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FileMode = System.IO.FileMode;

namespace Wabbajack.Networking.WabbajackClientApi;

public class Client
{
    public static readonly long UploadedFileBlockSize = (long) 1024 * 1024 * 2;

    private readonly HttpClient _client;
    private readonly Configuration _configuration;
    private readonly DTOSerializer _dtos;
    private readonly IResource<Client> _hashLimiter;
    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger<Client> _logger;

    private readonly ITokenProvider<WabbajackApiState> _token;
    private bool _inited;

    public bool IgnoreMirrorList { get; set; } = false;

    public Client(ILogger<Client> logger, HttpClient client, ITokenProvider<WabbajackApiState> token,
        DTOSerializer dtos,
        IResource<HttpClient> limiter, IResource<Client> hashLimiter, Configuration configuration)
    {
        _configuration = configuration;
        _token = token;
        _client = client;
        _logger = logger;
        _dtos = dtos;
        _limiter = limiter;
        _hashLimiter = hashLimiter;
        _inited = false;
    }

    private async ValueTask<HttpRequestMessage> MakeMessage(HttpMethod method, Uri uri, HttpContent? content = null)
    {
        var msg = new HttpRequestMessage(method, uri);
        var key = (await _token.Get())!;
        msg.Headers.Add(_configuration.MetricsKeyHeader, key.MetricsKey);
        if (!string.IsNullOrWhiteSpace(key.AuthorKey))
            msg.Headers.Add(_configuration.AuthorKeyHeader, key.AuthorKey);

        if (content != null)
            msg.Content = content;
        return msg;
    }

    public async Task SendMetric(string action, string subject, bool rebound = true)
    {
        if (!_inited)
        {
            _logger.LogInformation("Init Client: {Id}", (await _token.Get())?.MetricsKey);
            _inited = true;
        }
        
        var msg = await MakeMessage(HttpMethod.Get,
            new Uri($"{_configuration.BuildServerUrl}metrics/{action}/{subject}"));
        var result = await _client.SendAsync(msg);
        if (rebound && result.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.InternalServerError)
        {
            _logger.LogError("HTTP Error: {Result}", result);
            await SendMetric("rebound", "Error", false);
            Environment.Exit(0);
        }
    }

    public async Task<ServerAllowList> LoadDownloadAllowList()
    {
        var str = await _client.GetStringAsync(_configuration.ServerAllowList);
        var d = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        return d.Deserialize<ServerAllowList>(str);
    }

    public async Task<Archive[]> LoadMirrors()
    {
        if (IgnoreMirrorList)
            return Array.Empty<Archive>();

        var str = await _client.GetStringAsync(_configuration.MirrorList);
        return JsonSerializer.Deserialize<Archive[]>(str, _dtos.Options) ?? [];
    }

    public async Task<ServerAllowList> LoadMirrorAllowList()
    {
        var str = await _client.GetStringAsync(_configuration.MirrorAllowList);
        var d = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        return d.Deserialize<ServerAllowList>(str);
    }

    public async Task<Dictionary<Hash, ValidatedArchive>> LoadUpgradedArchives()
    {
        return (await _client.GetFromJsonAsync<ValidatedArchive[]>(_configuration.UpgradedArchives, _dtos.Options))!
            .ToDictionary(d => d.Original.Hash);
    }

    public async Task<Archive[]> GetGameArchives(Game game, string version)
    {
        var url = $"https://raw.githubusercontent.com/wabbajack-tools/indexed-game-files/master/{game}/{version}.json";
        _logger.LogInformation($"The URL for retrieving game file hashes is : {url}");
        return await _client.GetFromJsonAsync<Archive[]>(url, _dtos.Options) ?? Array.Empty<Archive>();
    }

    public async Task<Archive[]> GetArchivesForHash(Hash hash)
    {
        var msg = await MakeMessage(HttpMethod.Get,
            new Uri($"{_configuration.BuildServerUrl}mod_files/by_hash/{hash.ToHex()}"));
        return await _client.GetFromJsonAsync<Archive[]>(_limiter, msg, _dtos.Options) ?? Array.Empty<Archive>();
    }

    public Uri GetMirrorUrl(Hash archiveHash)
    {
        return new Uri($"{_configuration.MirrorServerUrl}{archiveHash.ToHex()}");
    }

    public async Task SendModListDefinition(ModList modList)
    {
        await using var fs = new MemoryStream();
        await using var gzip = new GZipStream(fs, CompressionLevel.SmallestSize, true);
        await _dtos.Serialize(modList, gzip);
        await gzip.DisposeAsync();
        fs.Position = 0;

        var msg = new HttpRequestMessage(HttpMethod.Post,
            $"{_configuration.BuildServerUrl}list_definitions/ingest");
        msg.Headers.Add("x-compressed-body", "gzip");
        msg.Content = new StreamContent(fs);
        await _client.SendAsync(msg);
    }

    public async Task<ModListSummary[]> GetListStatuses()
    {
        return await _client.GetFromJsonAsync<ModListSummary[]>(
            "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/modListSummary.json",
            _dtos.Options) ?? Array.Empty<ModListSummary>();
    }

    public async Task<ValidatedModList> GetDetailedStatus(string machineURL)
    {
        return (await _client.GetFromJsonAsync<ValidatedModList>(
            $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{machineURL}/status.json",
            _dtos.Options))!;
    }

    IEnumerable<PartDefinition> Blocks(long size)
    {
        for (long block = 0; block * UploadedFileBlockSize < size; block++)
            yield return new PartDefinition
            {
                Index = block,
                Size = Math.Min(UploadedFileBlockSize, size - block * UploadedFileBlockSize),
                Offset = block * UploadedFileBlockSize
            };
    }


    public async Task<FileDefinition> GenerateFileDefinition(AbsolutePath path)
    {

        var parts = Blocks(path.Size()).ToArray();
        var definition = new FileDefinition
        {
            OriginalFileName = path.FileName,
            Size = path.Size(),
            Hash = await path.Hash(),
            Parts = await parts.PMapAll(async part =>
            {
                var buffer = new byte[part.Size];
                using var job = await _hashLimiter.Begin("Hashing part", part.Size, CancellationToken.None);
                await using (var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Position = part.Offset;
                    await fs.ReadAsync(buffer);
                }

                part.Hash = await buffer.Hash(job);
                return part;
            }).ToArray()
        };

        return definition;
    }

    public async Task<ModlistMetadata[]> LoadLists()
    {
        var repos = LoadRepositories();
        var featured = await LoadFeaturedLists();

        return await (await repos).PMapAll(async url =>
            {
                try
                {
                    return (await _client.GetFromJsonAsync<ModlistMetadata[]>(_limiter,
                        new HttpRequestMessage(HttpMethod.Get, url.Value),
                        _dtos.Options))!.Select(meta =>
                    {
                        meta.RepositoryName = url.Key;
                        meta.Official = (meta.RepositoryName == "wj-featured" ||
                                         featured.Contains(meta.NamespacedName));
                        return meta;
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "While loading {List} from {Url}", url.Key, url.Value);
                    return Enumerable.Empty<ModlistMetadata>();
                }
            })
            .SelectMany(x => x)
            .ToArray();
    }

    private async Task<HashSet<string>> LoadFeaturedLists()
    {
        var data = await _client.GetFromJsonAsync<string[]>(_limiter,
            new HttpRequestMessage(HttpMethod.Get,
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/featured_lists.json"),
            _dtos.Options);
        return data!.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
    }

    public async Task<Dictionary<string, Uri>> LoadRepositories()
    {
        var repositories = await _client.GetFromJsonAsync<Dictionary<string, Uri>>(_limiter,
            new HttpRequestMessage(HttpMethod.Get,
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/repositories.json"), _dtos.Options);
        return repositories!;
    }

    public async Task<SearchIndex> LoadSearchIndex()
    {
        return await _client.GetFromJsonAsync<SearchIndex>(_limiter,
            new HttpRequestMessage(HttpMethod.Get, 
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/refs/heads/master/reports/searchIndex.json"),
                _dtos.Options);
    }
    
    public Uri GetPatchUrl(Hash upgradeHash, Hash archiveHash)
    {
        return new Uri($"{_configuration.PatchBaseAddress}{upgradeHash.ToHex()}_{archiveHash.ToHex()}");
    }

    public async Task<ValidatedArchive> UploadPatch(ValidatedArchive validated, Stream data)
    {
        _logger.LogInformation("Uploading Patch {From} {To}", validated.Original.Hash, validated.PatchedFrom!.Hash);
        var name = $"{validated.Original.Hash.ToHex()}_{validated.PatchedFrom.Hash.ToHex()}";

        var blocks = Blocks(data.Length).ToArray();
        foreach (var block in blocks)
        {
            _logger.LogInformation("Uploading Block {Idx}/{Max}", block.Index, blocks.Length);
            data.Position = block.Offset;
            var blockData = new byte[block.Size];
            await data.ReadAsync(blockData);
            var hash = await blockData.Hash();

            using var result = await _client.SendAsync(await MakeMessage(HttpMethod.Post,
                new Uri($"{_configuration.BuildServerUrl}patches?name={name}&start={block.Offset}"),
                new ByteArrayContent(blockData)));
            if (!result.IsSuccessStatusCode)
                throw new HttpException(result);

            var resultHash = Hash.FromHex(await result.Content.ReadAsStringAsync());
            if (resultHash != hash)
                throw new Exception($"Result Hash does not match expected hash {hash} vs {resultHash}");
        }

        validated.PatchUrl = new Uri($"https://patches.wabbajack.org/{name}");

        return validated;
    }

    public async Task AddForceHealedPatch(ValidatedArchive validated)
    {
        var oldData =
            await GetGithubFile<ValidatedArchive[]>("wabbajack-tools", "mod-lists", "configs/forced_healing.json");
        var content = oldData.Content.Append(validated).ToArray();
        await UpdateGitHubFile("wabbajack-tools", "mod-lists", "configs/forced_healing.json", content, oldData.Sha);
    }

    private async Task UpdateGitHubFile<T>(string owner, string repo, string path, T content, string oldSha)
    {
        var json = _dtos.Serialize(content, writeIndented: true);
        var msg = await MakeMessage(HttpMethod.Post,
            new Uri($"{_configuration.BuildServerUrl}github/?owner={owner}&repo={repo}&path={path}&oldSha={oldSha}"));

        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var result = await _client.SendAsync(msg);
        if (!result.IsSuccessStatusCode)
            throw new HttpException(result);
    }

    private async Task<(string Sha, T Content)> GetGithubFile<T>(string owner, string repo, string path,
        CancellationToken? token = null)
    {
        token ??= CancellationToken.None;

        var msg = await MakeMessage(HttpMethod.Get,
            new Uri($"{_configuration.BuildServerUrl}github/?owner={owner}&repo={repo}&path={path}"));
        using var oldData = await _client.SendAsync(msg, token.Value);
        if (!oldData.IsSuccessStatusCode)
            throw new HttpException(oldData);

        var sha = oldData.Headers.GetValues(_configuration.ResponseShaHeader).First();
        return (sha, (await oldData.Content.ReadFromJsonAsync<T>(_dtos.Options, token.Value))!);
    }


    public async Task UploadMirror(FileDefinition definition, AbsolutePath file)
    {
        var hashAsHex = definition.Hash.ToHex();
        _logger.LogInformation("Starting upload of {Name} ({Hash})", file.FileName, hashAsHex);

        using var result = await _client.SendAsync(await MakeMessage(HttpMethod.Put,
            new Uri($"{_configuration.BuildServerUrl}mirrored_files/create/{hashAsHex}"),
            new StringContent(_dtos.Serialize(definition), Encoding.UTF8, "application/json")));
        if (!result.IsSuccessStatusCode)
            throw new HttpException(result);

        _logger.LogInformation("Uploading Parts");

        await using var dataIn = file.Open(FileMode.Open);

        foreach (var (part, idx) in definition.Parts.Select((part, idx) => (part, idx)))
        {
            _logger.LogInformation("Uploading Part {Part}/{Max}", idx, definition.Parts.Length);

            dataIn.Position = part.Offset;
            var data = new byte[part.Size];
            await dataIn.ReadAsync(data);

            using var partResult = await _client.SendAsync(await MakeMessage(HttpMethod.Put,
                new Uri($"{_configuration.BuildServerUrl}mirrored_files/{hashAsHex}/part/{idx}"),
                new ByteArrayContent(data)));

            if (!partResult.IsSuccessStatusCode)
                throw new HttpException(result);
        }

        using var finalResult = await _client.SendAsync(await MakeMessage(HttpMethod.Put,
            new Uri($"{_configuration.BuildServerUrl}mirrored_files/{hashAsHex}/finish")));

        if (!finalResult.IsSuccessStatusCode)
            throw new HttpException(result);
    }

    public async Task<FileDefinition[]> GetAllMirroredFileDefinitions(CancellationToken token)
    {
        return (await _client.GetFromJsonAsync<FileDefinition[]>($"{_configuration.BuildServerUrl}mirrored_files",
            _dtos.Options, token))!;
    }

    public async Task<ValidatedArchive[]> GetAllPatches(CancellationToken token)
    {
        return (await _client.GetFromJsonAsync<ValidatedArchive[]>(
            "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/configs/forced_healing.json",
            _dtos.Options, token))!;
    }

    public async Task DeleteMirror(Hash hash)
    {
        _logger.LogInformation("Deleting mirror of {Hash}", hash);
        var msg = await MakeMessage(HttpMethod.Delete,
            new Uri($"{_configuration.BuildServerUrl}mirrored_files/{hash.ToHex()}"));
        var result = await _client.SendAsync(msg);
        if (!result.IsSuccessStatusCode)
            throw new HttpException(result);
    }


    public async Task<(IObservable<(Percent PercentDone, string Message)> Progress, Task<Uri> Task)> UploadAuthorFile(
        AbsolutePath path)
    {
        var apiKey = (await _token.Get())!.AuthorKey;
        var report = new Subject<(Percent PercentDone, string Message)>();

        var tsk = Task.Run<Uri>(async () =>
        {
            report.OnNext((Percent.Zero, "Generating File Definition"));
            var definition = await GenerateFileDefinition(path);

            report.OnNext((Percent.Zero, "Creating file upload"));
            await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
            {
                var msg = await MakeMessage(HttpMethod.Put,
                    new Uri($"{_configuration.BuildServerUrl}authored_files/create"));
                msg.Content = new StringContent(_dtos.Serialize(definition));
                using var result = await _client.SendAsync(msg);
                HttpException.ThrowOnFailure(result);
                definition.ServerAssignedUniqueId = await result.Content.ReadAsStringAsync();
            });

            report.OnNext((Percent.Zero, "Starting part uploads"));
            await definition.Parts.PDoAll(_limiter, async part =>
            {
                report.OnNext((Percent.FactoryPutInRange(part.Index, definition.Parts.Length),
                    $"Uploading Part ({part.Index}/{definition.Parts.Length})"));
                var buffer = new byte[part.Size];
                await using (var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Position = part.Offset;
                    await fs.ReadAsync(buffer);
                }

                await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
                {
                    var msg = await MakeMessage(HttpMethod.Put,
                        new Uri(
                            $"{_configuration.BuildServerUrl}authored_files/{definition.ServerAssignedUniqueId}/part/{part.Index}"));
                    msg.Content = new ByteArrayContent(buffer);
                    using var putResult = await _client.SendAsync(msg);
                    HttpException.ThrowOnFailure(putResult);
                    var hash = Hash.FromBase64(await putResult.Content.ReadAsStringAsync());
                    if (hash != part.Hash)
                        throw new InvalidDataException("Hashes don't match");
                    return hash;
                });

            });

            report.OnNext((Percent.Zero, "Finalizing upload"));
            return await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
            {
                var msg = await MakeMessage(HttpMethod.Put,
                    new Uri(
                        $"{_configuration.BuildServerUrl}authored_files/{definition.ServerAssignedUniqueId}/finish"));
                msg.Content = new StringContent(_dtos.Serialize(definition));
                using var result = await _client.SendAsync(msg);
                HttpException.ThrowOnFailure(result);
                report.OnNext((Percent.One, "Finished"));
                return new Uri($"https://authored-files.wabbajack.org/{definition.MungedName}");
            });
        });
        return (report, tsk);
    }

    public async Task<ForcedRemoval[]> GetForcedRemovals(CancellationToken token)
    {
        return (await _client.GetFromJsonAsync<ForcedRemoval[]>(
            "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/configs/forced_removal.json",
            _dtos.Options, token))!;
    }

    public async Task<SteamManifest[]> GetSteamManifests(Game game, string version)
    {
        var url =
            $"https://raw.githubusercontent.com/wabbajack-tools/indexed-game-files/master/{game}/{version}_steam_manifests.json";
        return await _client.GetFromJsonAsync<SteamManifest[]>(url, _dtos.Options) ?? Array.Empty<SteamManifest>();
    }

    public async Task<bool> ProxyHas(Uri uri)
    {
        var newUri = new Uri($"{_configuration.BuildServerUrl}proxy?uri={HttpUtility.UrlEncode(uri.ToString())}");
        var msg = new HttpRequestMessage(HttpMethod.Head, newUri);
        try
        {
            var result = await _client.SendAsync(msg);
            return result.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async ValueTask<Uri?> MakeProxyUrl(Archive archive, Uri uri)
    {
        if (archive.State is Manual && !await ProxyHas(uri))
            return null;

        return new Uri(
            $"{_configuration.BuildServerUrl}proxy?name={archive.Name}&hash={archive.Hash.ToHex()}&uri={HttpUtility.UrlEncode(uri.ToString())}");
    }

    public async Task<IndexedVirtualFile?> GetCesiVfsEntry(Hash hash, CancellationToken token)
    {
        var msg = await MakeMessage(HttpMethod.Get, new Uri($"{_configuration.BuildServerUrl}cesi/vfs/{hash.ToHex()}"));
        using var response = await _client.SendAsync(msg, token);
        HttpException.ThrowOnFailure(response);
        return await _dtos.DeserializeAsync<IndexedVirtualFile>(await response.Content.ReadAsStreamAsync(token), token);
    }

    public async Task<IReadOnlyList<string>> GetMyModlists(CancellationToken token)
    {
        var msg = await MakeMessage(HttpMethod.Get, new Uri($"{_configuration.BuildServerUrl}author_controls/lists"));
        using var response = await _client.SendAsync(msg, token);
        HttpException.ThrowOnFailure(response);
        return (await _dtos.DeserializeAsync<string[]>(await response.Content.ReadAsStreamAsync(token), token))!;
    }

    public async Task PublishModlist(string namespacedName, Version version,  AbsolutePath modList, DownloadMetadata metadata)
    {
        var pair = namespacedName.Split("/");
        var wjRepoName = pair[0];
        var machineUrl = pair[1];

        var repoUrl = (await LoadRepositories())[wjRepoName];

        var decomposed = repoUrl.LocalPath.Split("/");
        var owner = decomposed[1];
        var repoName = decomposed[2];
        var path = string.Join("/", decomposed[4..]);
        
        _logger.LogInformation("Uploading modlist {MachineUrl}", namespacedName);
        
        var (progress, uploadTask) = await UploadAuthorFile(modList);
        progress.Subscribe(x => _logger.LogInformation(x.Message));
        var downloadUrl = await uploadTask;
        
        _logger.LogInformation("Publishing modlist {MachineUrl}", namespacedName);

        var creds = new Credentials((await _token.Get())!.AuthorKey);
        var ghClient = new GitHubClient(new ProductHeaderValue("wabbajack")) {Credentials = creds};

        var oldData =
            (await ghClient.Repository.Content.GetAllContents(owner, repoName, path))
            .First();
        var oldContent = _dtos.Deserialize<ModlistMetadata[]>(oldData.Content);
        var list = oldContent.First(c => c.Links.MachineURL == machineUrl);
        list.Version = version;
        list.DownloadMetadata = metadata;
        list.Links.Download = downloadUrl.ToString();
        list.DateUpdated = DateTime.UtcNow;


        var newContent = _dtos.Serialize(oldContent, true);
        // the website requires all names be in lowercase;
        newContent = GameRegistry.Games.Keys.Aggregate(newContent,
            (current, g) => current.Replace($"\"game\": \"{g}\",", $"\"game\": \"{g.ToString().ToLower()}\","));

        var updateRequest = new UpdateFileRequest($"New release of {machineUrl}", newContent, oldData.Sha);
        await ghClient.Repository.Content.UpdateFile(owner, repoName, path, updateRequest);
    }
}
