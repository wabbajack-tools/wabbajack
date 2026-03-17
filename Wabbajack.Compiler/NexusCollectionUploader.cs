using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using static Wabbajack.Compiler.WabbajackToVortexCollection;
using FileMode = System.IO.FileMode;

namespace Wabbajack.Compiler
{
    public class NexusCollectionUploader
    {
        private sealed class ManifestModSource
        {
            [JsonPropertyName("type")] public string type { get; set; } = "nexus";
            [JsonPropertyName("mod_id")] public string mod_id { get; set; } = "";
            [JsonPropertyName("file_id")] public string file_id { get; set; } = "";
            [JsonPropertyName("url")] public string url { get; set; } = "";
            [JsonPropertyName("md5")] public string md5 { get; set; } = "";
            [JsonPropertyName("file_size")] public long file_size { get; set; } = 0;
            [JsonPropertyName("update_policy")] public string update_policy { get; set; } = "";
            [JsonPropertyName("logical_filename")] public string logical_filename { get; set; } = "";
            [JsonPropertyName("file_expression")] public string file_expression { get; set; } = "";
            [JsonPropertyName("adult_content")] public bool adult_content { get; set; } = false;
        }

        private sealed class ManifestMod
        {
            [JsonPropertyName("name")] public string name { get; set; } = "Unknown";
            [JsonPropertyName("version")] public string version { get; set; } = "1.0.0";
            [JsonPropertyName("optional")] public bool optional { get; set; } = false;
            [JsonPropertyName("domain_name")] public string domain_name { get; set; } = "";
            [JsonPropertyName("source")] public ManifestModSource source { get; set; } = new();
            [JsonPropertyName("author")] public string author { get; set; } = "";
        }

        private readonly ILogger _logger;
        private readonly ITokenProvider<NexusOAuthState> _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public delegate void UploadProgressHandler(string stage, double progress);
        public event UploadProgressHandler? OnProgress;

        private static readonly string RestBaseUrl =
            Environment.GetEnvironmentVariable("NEXUS_REST_URL")
            ?? "https://api.nexusmods.com/v3";

        private static readonly string GraphQLUrl =
            Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL")
            ?? "https://api.nexusmods.com/v2/graphql";

        private const int CollectionSchemaId = 2;
        private const int MaxUploadAttempts = 3;

        public NexusCollectionUploader(
            ILogger logger,
            ITokenProvider<NexusOAuthState> tokenProvider,
            HttpClient httpClient,
            JsonSerializerOptions jsonOptions)
        {
            _logger = logger;
            _tokenProvider = tokenProvider;
            _httpClient = httpClient;
            _jsonOptions = jsonOptions;
        }


        public async Task<CollectionUploadResult?> UploadCollection(
            ModList modList,
            AbsolutePath collectionJsonPath,
            AbsolutePath archivePath,
            string? existingCollectionId = null,
            string? gameVersion = null,
            Func<Task<bool>>? confirmFallbackToCreate = null,
            CancellationToken token = default)
        {
            try
            {
                if (!_tokenProvider.HaveToken())
                {
                    _logger.LogError("No Nexus Mods OAuth token available. Please log in first.");
                    return null;
                }

                var authState = await _tokenProvider.Get();
                if (authState?.OAuth?.IsExpired ?? true)
                {
                    _logger.LogError("Nexus Mods OAuth token is expired. Please log in again.");
                    return null;
                }


                string? uploadUuid = null;

                for (int uploadAttempt = 1; uploadAttempt <= MaxUploadAttempts; uploadAttempt++)
                {
                    OnProgress?.Invoke("requesting_url", 0.0);
                    _logger.LogInformation(
                        "Requesting multipart upload session from Nexus Mods (attempt {attempt}/{max})...",
                        uploadAttempt, MaxUploadAttempts);

                    OnProgress?.Invoke("upload", 0.0);
                    _logger.LogInformation(
                        "Uploading collection archive ({size:N0} bytes, {mb:N0} MB)...",
                        archivePath.Size(), archivePath.Size() / (1024 * 1024));

                    uploadUuid = await UploadFileMultipart(archivePath, authState.OAuth.AccessToken, token);

                    if (uploadUuid != null) break;

                    if (uploadAttempt < MaxUploadAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, uploadAttempt) * 5);
                        _logger.LogWarning("Upload attempt {attempt} failed. Retrying in {delay}s...",
                            uploadAttempt, delay.TotalSeconds);
                        await Task.Delay(delay, token);
                    }
                }

                if (uploadUuid == null)
                {
                    _logger.LogError("File upload failed after {max} attempts", MaxUploadAttempts);
                    return null;
                }

                _logger.LogInformation("File uploaded successfully (UUID: {uuid})", uploadUuid);


                OnProgress?.Invoke("finalising_upload", 0.0);
                if (!await FinaliseUpload(uploadUuid, authState.OAuth.AccessToken, token))
                {
                    _logger.LogError("Failed to finalise upload {uuid}", uploadUuid);
                    return null;
                }

                if (!await WaitForUploadAvailable(uploadUuid, authState.OAuth.AccessToken, token))
                {
                    _logger.LogError("Upload {uuid} did not reach 'available' state in time", uploadUuid);
                    return null;
                }


                OnProgress?.Invoke("building_manifest", 0.0);
                var collectionPayload = WabbajackToVortexCollection.Build(modList, gameVersion);

                OnProgress?.Invoke("sending_manifest", 0.0);
                _logger.LogInformation(
                    "Creating/updating collection on Nexus Mods (existingCollectionId={id})",
                    existingCollectionId ?? "none");

                CollectionUploadResult? result = null;

                if (!string.IsNullOrWhiteSpace(existingCollectionId))
                {
                    result = await CreateCollectionRevision(
                        collectionPayload, uploadUuid, existingCollectionId,
                        modList.IsNSFW, authState.OAuth.AccessToken, collectionJsonPath, token);

                    if (result == null)
                        _logger.LogWarning(
                            "createCollectionRevision failed for collectionId={id}.", existingCollectionId);
                }

                if (result == null)
                {
                    if (!string.IsNullOrWhiteSpace(existingCollectionId) && confirmFallbackToCreate != null)
                    {
                        var confirmed = await confirmFallbackToCreate();
                        if (!confirmed)
                        {
                            _logger.LogInformation("Fallback to createCollection declined by caller. Aborting.");
                            return null;
                        }
                    }

                    result = await CreateCollection(
                        collectionPayload, uploadUuid, modList.IsNSFW,
                        authState.OAuth.AccessToken, collectionJsonPath, token);
                }

                if (result != null && result.Success)
                {
                    _logger.LogInformation(
                        "Collection uploaded successfully! CollectionId: {id}, RevisionId: {rev}, Slug: {slug}",
                        result.CollectionId, result.RevisionId, result.Slug);

                    OnProgress?.Invoke("complete", 1.0);
                }
                else
                {
                    _logger.LogError("Collection upload failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload collection");
                return null;
            }
        }


        private async Task<MultipartUploadSession?> CreateMultipartUploadSession(
            AbsolutePath filePath, string accessToken, CancellationToken token)
        {
            var payload = new
            {
                size_bytes = filePath.Size(),
                filename = filePath.FileName.ToString()
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{RestBaseUrl}/uploads/multipart")
            { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<MultipartUploadSession>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private async Task<string?> UploadFileMultipart(
            AbsolutePath filePath, string accessToken, CancellationToken token)
        {
            var session = await CreateMultipartUploadSession(filePath, accessToken, token);
            if (session == null)
            {
                _logger.LogError("Failed to create multipart upload session");
                return null;
            }

            var sessionId = session.SessionUuid;
            _logger.LogInformation(
                "Multipart session created: UUID={uuid}, Parts={parts}, PartSize={size}MB",
                sessionId, session.PartsPresignedUrl.Count, session.PartsSize / 1024 / 1024);

            var totalSize = filePath.Size();
            var etags = new ConcurrentDictionary<int, string>();
            var completed = 0;

            using var uploadClient = CreateUploadClient();

            for (int i = 0; i < session.PartsPresignedUrl.Count; i++)
            {
                var partNumber = i + 1;
                var partUrl = session.PartsPresignedUrl[i];
                var offset = (long)i * session.PartsSize;
                var length = (int)Math.Min(session.PartsSize, totalSize - offset);

                var etag = await UploadPartWithRetry(
                    uploadClient, filePath, partUrl, partNumber, offset, length, totalSize, token);

                if (etag == null)
                {
                    _logger.LogError("Part {part} failed permanently", partNumber);
                    return null;
                }

                etags[partNumber] = etag;
                completed++;
                var pct = (double)completed / session.PartsPresignedUrl.Count;
                OnProgress?.Invoke("upload", pct);
                _logger.LogInformation("Part {part}/{total} complete | {pct:P0}",
                    partNumber, session.PartsPresignedUrl.Count, pct);
            }


            var success = await CompleteMultipartUpload(
                uploadClient,
                session.CompletePresignedUrl,
                etags.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)).ToList(),
                token);

            return success ? sessionId : null;
        }

        private async Task<string?> UploadPartWithRetry(
            HttpClient client, AbsolutePath filePath, string url,
            int partNumber, long offset, int length, long totalSize, CancellationToken token)
        {
            const int maxRetries = 10;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var chunk = new byte[length];
                    await using var f = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                    f.Seek(offset, SeekOrigin.Begin);
                    await f.ReadExactlyAsync(chunk, token);

                    using var content = new ByteArrayContent(chunk);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Headers.ContentLength = length;

                    using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
                    using var response = await client.SendAsync(request, linked.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var etag = response.Headers.ETag?.Tag?.Trim('"') ?? "";
                        if (string.IsNullOrEmpty(etag))
                            throw new Exception("No ETag in response");

                        _logger.LogDebug("Part {part} OK (attempt {attempt}) ETag={etag}",
                            partNumber, attempt, etag);
                        return etag;
                    }

                    _logger.LogWarning("Part {part} attempt {attempt}: HTTP {status}",
                        partNumber, attempt, (int)response.StatusCode);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning("Part {part} attempt {attempt} failed: {msg}",
                        partNumber, attempt, ex.Message);
                }

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(
                        Math.Min(Math.Pow(2, attempt), 60) + Random.Shared.NextDouble() * 5);
                    _logger.LogInformation("Retrying part {part} in {delay:N1}s...",
                        partNumber, delay.TotalSeconds);
                    await Task.Delay(delay, token);
                }
            }

            return null;
        }

        private async Task<bool> CompleteMultipartUpload(
            HttpClient client, string completeUrl,
            List<(int PartNumber, string ETag)> etags, CancellationToken token)
        {
            var xml = new StringBuilder("<CompleteMultipartUpload>");
            foreach (var (partNumber, etag) in etags)
                xml.Append(
                    $"<Part><PartNumber>{partNumber}</PartNumber><ETag>{etag}</ETag></Part>");
            xml.Append("</CompleteMultipartUpload>");

            using var content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml");
            using var request = new HttpRequestMessage(HttpMethod.Post, completeUrl)
            {
                Content = content
            };

            var response = await client.SendAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Multipart completion failed: {status} - {body}",
                    (int)response.StatusCode, body);
                return false;
            }

            _logger.LogInformation("Multipart upload completed successfully");
            return true;
        }

        private async Task<bool> FinaliseUpload(
            string uploadUuid, string accessToken, CancellationToken token)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{RestBaseUrl}/uploads/{uploadUuid}/finalise")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            AddNexusHeaders(request, accessToken);

            try
            {
                var response = await _httpClient.SendAsync(request, token);
                var body = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FinaliseUpload failed: {status} - {body}",
                        response.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("Upload {uuid} finalised", uploadUuid);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during FinaliseUpload for {uuid}", uploadUuid);
                return false;
            }
        }

        private async Task<bool> WaitForUploadAvailable(
            string uploadUuid, string accessToken, CancellationToken token,
            int maxWaitSeconds = 120)
        {
            var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
            var attempt = 0;

            while (DateTime.UtcNow < deadline)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(3 * attempt, 15)), token);

                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"{RestBaseUrl}/uploads/{uploadUuid}");
                AddNexusHeaders(request, accessToken);

                try
                {
                    var response = await _httpClient.SendAsync(request, token);
                    var body = await response.Content.ReadAsStringAsync(token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("GetUpload {uuid} returned {status}",
                            uploadUuid, response.StatusCode);
                        continue;
                    }

                    var root = JsonNode.Parse(body) as JsonObject;
                    var state = root?["state"]?.GetValue<string>();

                    _logger.LogInformation("Upload {uuid} state: {state} (attempt {attempt})",
                        uploadUuid, state, attempt);

                    if (state == "available") return true;

                    if (state != null && state != "created" && state != "processing")
                    {
                        _logger.LogError("Upload {uuid} entered unexpected state: {state}",
                            uploadUuid, state);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling upload state for {uuid}", uploadUuid);
                }
            }

            _logger.LogError("Upload {uuid} did not become available within {sec}s",
                uploadUuid, maxWaitSeconds);
            return false;
        }


        private Task<CollectionUploadResult?> CreateCollection(
            VortexCollection collectionPayload, string uploadUuid, bool adultContent,
            string accessToken, AbsolutePath collectionJsonPath, CancellationToken token)
            => ExecuteCollectionRestCall(
                $"{RestBaseUrl}/collections",
                collectionPayload, uploadUuid, adultContent,
                accessToken, collectionJsonPath,
                isRevision: false, collectionId: null, token: token);

        private Task<CollectionUploadResult?> CreateCollectionRevision(
            VortexCollection collectionPayload, string uploadUuid, string collectionId,
            bool adultContent, string accessToken, AbsolutePath collectionJsonPath,
            CancellationToken token)
            => ExecuteCollectionRestCall(
                $"{RestBaseUrl}/collections/{collectionId}/revisions",
                collectionPayload, uploadUuid, adultContent,
                accessToken, collectionJsonPath,
                isRevision: true, collectionId: collectionId, token: token);


        private List<ManifestMod>? BuildManifestMods(
            VortexCollection collectionPayload, string fallbackDomain)
        {
            var manifestMods = (collectionPayload.Mods ?? new List<VortexMod>())
                .Select(mod => new ManifestMod
                {
                    name = string.IsNullOrWhiteSpace(mod.Name) ? "Unknown" : mod.Name,
                    version = string.IsNullOrWhiteSpace(mod.Version) ? "1.0.0" : mod.Version,
                    optional = mod.Optional,
                    domain_name = string.IsNullOrWhiteSpace(mod.DomainName)
                                  ? fallbackDomain : mod.DomainName,
                    author = string.IsNullOrWhiteSpace(mod.Author) ? "" : mod.Author,
                    source = new ManifestModSource
                    {
                        type = mod.Source.Type,
                        mod_id = mod.Source.Type == "nexus"
                                           ? mod.Source.ModId.ToString() : "",
                        file_id = mod.Source.Type == "nexus"
                                           ? mod.Source.FileId.ToString() : "",
                        url = mod.Source.Url ?? "",
                        file_size = mod.Source.FileSize,
                        update_policy = "prefer",
                        logical_filename = mod.Source.LogicalFilename ?? "",
                        file_expression = mod.Source.LogicalFilename ?? "",
                        md5 = mod.Source.Md5 ?? "",
                        adult_content = false,
                    }
                })
                .ToList();


            var invalid = manifestMods
                .Where(m => m.source.type == "nexus" &&
                            (string.IsNullOrWhiteSpace(m.source.mod_id) || m.source.mod_id == "0" ||
                             string.IsNullOrWhiteSpace(m.source.file_id) || m.source.file_id == "0"))
                .Take(5)
                .ToList();

            if (invalid.Count > 0)
            {
                _logger.LogError(
                    "Some nexus-type mods are missing mod_id/file_id. First few: {mods}",
                    JsonSerializer.Serialize(
                        invalid.Select(m => new { m.name, m.source.mod_id, m.source.file_id }),
                        _jsonOptions));
                return null;
            }

            _logger.LogInformation(
                "Manifest: {nexus} nexus-type, {browse} browse-type, {total} total mods",
                manifestMods.Count(m => m.source.type == "nexus"),
                manifestMods.Count(m => m.source.type == "browse"),
                manifestMods.Count);

            return manifestMods;
        }


        private bool TryRemoveInvalidModsFromRestError(
            string responseBody, ref List<ManifestMod> manifestMods)
        {
            var badModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var root = JsonNode.Parse(responseBody) as JsonObject;
                var errors = root?["errors"] as JsonArray;

                if (errors != null && errors.Count > 0)
                {
                    foreach (var errNode in errors)
                    {
                        // Each entry: { "detail": "Mod {mod_id}, {domain} not available/not found", "pointer": "..." }
                        var msg = (errNode as JsonObject)?["detail"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(msg)) continue;

                        if (msg.StartsWith("Mod ", StringComparison.OrdinalIgnoreCase))
                        {
                            var afterMod = msg[4..]; // strip "Mod "
                            var commaIdx = afterMod.IndexOf(',');
                            var modIdStr = commaIdx > 0
                                ? afterMod[..commaIdx].Trim()
                                : afterMod.Trim();

                            if (!string.IsNullOrWhiteSpace(modIdStr))
                            {
                                badModIds.Add(modIdStr);
                                _logger.LogWarning("Nexus flagged invalid mod_id={id}: {msg}", modIdStr, msg);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse 422 details for invalid mod stripping");
            }

            if (badModIds.Count == 0)
            {
                _logger.LogWarning(
                    "Received 422 but could not extract any mod ids from response body: {body}",
                    responseBody);
                return false;
            }

            var before = manifestMods.Count;
            manifestMods = manifestMods
                .Where(m => !badModIds.Contains(m.source.mod_id))
                .ToList();

            var removed = before - manifestMods.Count;
            if (removed > 0)
                _logger.LogWarning(
                    "Removed {count} invalid mod(s) from manifest (mod_ids: [{ids}]); will retry",
                    removed, string.Join(", ", badModIds));
            else
                _logger.LogWarning(
                    "Nexus reported invalid mod_ids [{ids}] but none matched manifest entries — " +
                    "mod_ids in manifest may differ from those in the error. Cannot strip.",
                    string.Join(", ", badModIds));

            return removed > 0;
        }

        private async Task<CollectionUploadResult?> ExecuteCollectionRestCall(
            string url,
            VortexCollection collectionPayload,
            string uploadUuid,
            bool adultContent,
            string accessToken,
            AbsolutePath collectionJsonPath,
            bool isRevision,
            string? collectionId,
            CancellationToken token)
        {
            var info = collectionPayload.Info;

            if (string.IsNullOrWhiteSpace(info.DomainName) || info.DomainName == "site")
                throw new InvalidOperationException($"Invalid domain name: '{info.DomainName}'");

            const int MinNameLength = 3;
            const int MaxNameLength = 36;

            var name = string.IsNullOrWhiteSpace(info.Name) ? "Wabbajack Collection" : info.Name;
            if (name.Length > MaxNameLength)
            {
                _logger.LogWarning("Collection name too long (max {max}), truncating: '{name}'",
                    MaxNameLength, name);
                name = name[..MaxNameLength];
            }
            if (name.Length < MinNameLength)
                throw new InvalidOperationException($"Collection name too short: '{name}'");

            var manifestMods = BuildManifestMods(collectionPayload, info.DomainName);
            if (manifestMods == null) return null;
            if (manifestMods.Count == 0)
                throw new InvalidOperationException("Cannot create a Nexus collection with 0 mods.");

            // strips those mods and re-sends. Should only require 1-2 attempts
            const int MaxRestAttempts = 5;

            for (int attempt = 1; attempt <= MaxRestAttempts; attempt++)
            {
                var jsonBody = BuildJsonBody(uploadUuid, name, info, adultContent, manifestMods);

                var debugPath = collectionJsonPath.Parent.Combine(
                    $"{collectionJsonPath.FileName}_rest_payload.json");
                await debugPath.WriteAllTextAsync(jsonBody);
                _logger.LogInformation(
                    "Saved REST payload to {path} ({kb} KB, {count} mods)",
                    debugPath, jsonBody.Length / 1024, manifestMods.Count);

                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                AddNexusHeaders(request, accessToken);

                using var mutationCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    token, mutationCts.Token);

                string responseBody;
                System.Net.HttpStatusCode statusCode;

                try
                {
                    using var response = await _httpClient.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                    _logger.LogInformation("Response status: {status}", response.StatusCode);

                    statusCode = response.StatusCode;
                    responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Collection REST call failed: {status} - {body}",
                            statusCode, responseBody);

                        if (statusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                        {
                            if (attempt < MaxRestAttempts &&
                                TryRemoveInvalidModsFromRestError(responseBody, ref manifestMods))
                            {
                                if (manifestMods.Count == 0)
                                {
                                    _logger.LogError(
                                        "After removing invalid mods, 0 mods remain. Aborting.");
                                    return null;
                                }

                                _logger.LogWarning(
                                    "Retrying with {count} mods after stripping invalid refs " +
                                    "(attempt {next}/{max})",
                                    manifestMods.Count, attempt + 1, MaxRestAttempts);
                                continue;
                            }

                            return null;
                        }

                        if (statusCode == System.Net.HttpStatusCode.GatewayTimeout && attempt < MaxRestAttempts)
                        {
                            _logger.LogWarning(
                                "Gateway timeout on attempt {attempt}, retrying in 20s...", attempt);
                            await Task.Delay(TimeSpan.FromSeconds(20), token);
                            continue;
                        }

                        return null;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (TaskCanceledException) when (attempt < MaxRestAttempts)
                {
                    _logger.LogWarning(
                        "Collection REST request timed out on attempt {attempt}, retrying...", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(20), token);
                    continue;
                }
                catch (Exception ex) when (attempt < MaxRestAttempts)
                {
                    _logger.LogWarning(ex,
                        "Error during collection REST call attempt {attempt}, retrying...", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(20), token);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error during collection REST call attempt {attempt}, no more retries", attempt);
                    return null;
                }

                _logger.LogInformation(
                    "Collection REST call succeeded on attempt {attempt}. Response: {body}",
                    attempt, responseBody);

                try
                {
                    var root = JsonNode.Parse(responseBody) as JsonObject;

                    string returnedCollectionId;
                    string returnedRevisionId;

                    if (isRevision)
                    {
                        returnedRevisionId = root?["id"]?.GetValue<string>() ?? "";
                        returnedCollectionId = root?["collection_id"]?.GetValue<string>()
                                               ?? collectionId ?? "";
                    }
                    else
                    {
                        returnedCollectionId = root?["id"]?.GetValue<string>() ?? "";
                        returnedRevisionId = root?["revision_id"]?.GetValue<string>() ?? "";
                    }

                    var found = await FindRecentlyCreatedCollection(
                        info.Name, info.DomainName, accessToken, token,
                        preferredCollectionId: returnedCollectionId);

                    return new CollectionUploadResult
                    {
                        CollectionId = returnedCollectionId,
                        RevisionId = returnedRevisionId,
                        Slug = found?.Slug ?? "",
                        RevisionNumber = found?.RevisionNumber ?? 1,
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to parse collection REST response: {body}", responseBody);
                    return null;
                }
            }

            _logger.LogError("Exceeded retry attempts for collection REST call to {url}", url);
            return null;
        }


        private string BuildJsonBody(
            string uploadUuid,
            string name,
            VortexInfo info,
            bool adultContent,
            List<ManifestMod> manifestMods)
        {
            var manifestInfo = new
            {
                author = string.IsNullOrWhiteSpace(info.Author) ? "Anonymous" : info.Author,
                author_url = info.AuthorUrl ?? "",
                name,
                summary = info.Summary?.Trim() ?? "",
                description = info.Description ?? "",
                domain_name = info.DomainName,
                game_versions = (info.GameVersions?.Count ?? 0) == 0 ? null : info.GameVersions,
            };

            var manifest = new { info = manifestInfo, mods = manifestMods };

            var serOpts = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            return JsonSerializer.Serialize(new
            {
                upload_id = uploadUuid,
                collection_data = new
                {
                    adult_content = adultContent,
                    collection_schema_id = CollectionSchemaId,
                    collection_manifest = manifest,
                },
            }, serOpts);
        }


        private async Task<CollectionUploadResult?> FindRecentlyCreatedCollection(
            string collectionName,
            string domainName,
            string accessToken,
            CancellationToken token,
            string? preferredCollectionId = null)
        {
            try
            {
                var targetName = collectionName.Length > 36 ? collectionName[..36] : collectionName;
                var deadline = DateTime.UtcNow.AddSeconds(90);
                var attempt = 0;

                while (DateTime.UtcNow < deadline)
                {
                    attempt++;
                    var found = await FindRecentlyCreatedCollection_MyCollectionsSafeList(
                        targetName, domainName, accessToken, token, preferredCollectionId);
                    if (found != null)
                        return found;
                    var delay = TimeSpan.FromSeconds(Math.Min(5 + (attempt * 2), 15));
                    _logger.LogInformation(
                        "Collection not visible yet; re-checking in {seconds}s (attempt {attempt})",
                        delay.TotalSeconds, attempt);
                    await Task.Delay(delay, token);
                }

                _logger.LogWarning(
                    "No collection found matching '{name}' in domain '{domain}' within 90s",
                    targetName, domainName);
                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching for existing collection");
                return null;
            }
        }

        private async Task<CollectionUploadResult?> FindRecentlyCreatedCollection_MyCollectionsSafeList(
            string targetName,
            string domainName,
            string accessToken,
            CancellationToken token,
            string? preferredCollectionId = null)
        {
            var query = @"
                query myCollections($count: Int!, $offset: Int!, $viewAdultContent: Boolean!, $viewUnlisted: Boolean!, $viewUnderModeration: Boolean!) {
                  myCollections(
                    count: $count,
                    offset: $offset,
                    viewAdultContent: $viewAdultContent,
                    viewUnlisted: $viewUnlisted,
                    viewUnderModeration: $viewUnderModeration,
                    sortBy: ""created_at"",
                    sortDirection: ""DESC""
                  ) {
                    nodes {
                      id
                      slug
                      name
                      game { domainName }
                      draftRevisionNumber
                    }
                  }
                }";

            var variables = new { count = 50, offset = 0, viewAdultContent = true, viewUnlisted = true, viewUnderModeration = true };
            var graphqlRequest = new { query, variables };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("myCollections request failed: {status}", response.StatusCode);
                return null;
            }

            JsonObject? root;
            try { root = JsonNode.Parse(responseBody) as JsonObject; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "myCollections returned unparseable JSON");
                return null;
            }

            var errors = root?["errors"] as JsonArray;
            if (errors is { Count: > 0 })
                _logger.LogWarning("GraphQL errors from myCollections: {errors}", errors.ToJsonString());

            var nodes = root?["data"]?["myCollections"]?["nodes"] as JsonArray;
            if (nodes == null || nodes.Count == 0)
            {
                _logger.LogDebug("myCollections returned 0 nodes");
                return null;
            }

            var candidates = new List<JsonObject>();
            foreach (var node in nodes)
            {
                if (node is not JsonObject obj) continue;
                var name = obj["name"]?.GetValue<string>();
                var gameDomain = obj["game"]?["domainName"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(gameDomain)) continue;
                if (!name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!gameDomain.Equals(domainName, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(obj);
            }

            if (candidates.Count == 0)
                return null;

            // When we know the id of the collection we just created, we MUST match by id.
            // Never fall back to a different same-named collection — that would store the
            // wrong slug in the mapping. Return null so the retry loop keeps polling until
            // the correct collection appears in myCollections.
            JsonObject? match;
            if (!string.IsNullOrWhiteSpace(preferredCollectionId))
            {
                match = candidates.FirstOrDefault(obj =>
                {
                    var idNode = obj["id"];
                    string? nodeId = null;
                    if (idNode is JsonValue jv)
                    {
                        if (jv.TryGetValue<string>(out var s)) nodeId = s;
                        else if (jv.TryGetValue<int>(out var i)) nodeId = i.ToString();
                        else if (jv.TryGetValue<long>(out var l)) nodeId = l.ToString();
                    }
                    return string.Equals(nodeId, preferredCollectionId, StringComparison.OrdinalIgnoreCase);
                });

                if (match == null)
                {
                    var foundIds = candidates
                        .Select(o => o["id"]?.ToJsonString()?.Trim('"') ?? "?")
                        .ToList();
                    _logger.LogInformation(
                        "preferredCollectionId={id} not yet visible in myCollections " +
                        "(found same-named collection(s) with id(s): [{ids}]); will retry",
                        preferredCollectionId, string.Join(", ", foundIds));
                    return null;
                }
            }
            else
            {
                match = candidates[0];
            }

            var slug2 = match["slug"]?.GetValue<string>();
            var id2 = match["id"]?.ToJsonString()?.Trim('"') ?? "";
            var draftRev = match["draftRevisionNumber"]?.GetValue<int?>() ?? 1;
            _logger.LogInformation(
                "Found matching collection: id={id} slug='{slug}' draftRev={draftRev}",
                id2, slug2, draftRev);
            return new CollectionUploadResult { CollectionId = id2, Slug = slug2 ?? "", RevisionId = "0", RevisionNumber = draftRev, Success = true };
        }

        private static int ParseIntId(JsonNode? idNode)
        {
            try
            {
                if (idNode == null) return 0;
                if (idNode is JsonValue jv)
                {
                    if (jv.TryGetValue<int>(out var i)) return i;
                    if (jv.TryGetValue<long>(out var l)) return l > int.MaxValue ? 0 : (int)l;
                    if (jv.TryGetValue<string>(out var s))
                    {
                        if (int.TryParse(s, out var pi)) return pi;
                        if (long.TryParse(s, out var pl)) return pl > int.MaxValue ? 0 : (int)pl;
                    }
                }
            }
            catch { }
            return 0;
        }

        public async Task<string?> ValidateTokenAsync(CancellationToken token)
        {
            if (!_tokenProvider.HaveToken())
                return "No Nexus Mods token found. Please log in via Settings.";

            var authState = await _tokenProvider.Get();
            if (authState?.OAuth == null)
                return "OAuth state is missing. Please log in via Settings.";

            if (authState.OAuth.IsExpired)
                return "Your Nexus Mods session has expired. Please log in again via Settings.";

            var query = @"query { currentUser { name } }";
            var graphqlRequest = new { query };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, _jsonOptions),
                Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl)
            {
                Content = content
            };
            AddNexusHeaders(request, authState.OAuth.AccessToken);

            try
            {
                using var validateCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    token, validateCts.Token);

                var response = await _httpClient.SendAsync(request, linked.Token);
                var body = await response.Content.ReadAsStringAsync(linked.Token);

                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                        or System.Net.HttpStatusCode.Forbidden)
                    return "Your Nexus Mods session is no longer valid. " +
                           "Please log in again via Settings.";

                if (!response.IsSuccessStatusCode)
                    return $"Nexus Mods API returned {(int)response.StatusCode} while " +
                           "validating your session. Please try again.";

                var root = JsonNode.Parse(body) as JsonObject;
                var errors = root?["errors"] as JsonArray;
                if (errors is { Count: > 0 })
                {
                    var firstCode = (errors[0] as JsonObject)?
                        ["extensions"]?["code"]?.GetValue<string>();
                    if (firstCode is "UNAUTHORIZED" or "FORBIDDEN" or "UNAUTHENTICATED")
                        return "Your Nexus Mods session is no longer valid. " +
                               "Please log in again via Settings.";
                }

                var userName = root?["data"]?["currentUser"]?["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(userName))
                    return "Could not confirm Nexus Mods identity. Please log in again via Settings.";

                _logger.LogInformation("Nexus token validated — logged in as: {user}", userName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation request failed");
                return $"Could not reach Nexus Mods to validate your session: {ex.Message}";
            }
        }


        private static void AddNexusHeaders(HttpRequestMessage request, string accessToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("User-Agent", "Wabbajack/0.0.0");
            request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
            request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
            request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");
        }

        private static HttpClient CreateUploadClient()
        {
            var handler = new SocketsHttpHandler
            {
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                InitialHttp2StreamWindowSize = 512 * 1024,
            };
            handler.Expect100ContinueTimeout = TimeSpan.Zero;
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }
    }


    public class MultipartUploadSession
    {
        [JsonPropertyName("uuid")] public string Uuid { get; set; } = "";
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("parts_size")] public long PartsSize { get; set; }
        [JsonPropertyName("parts_presigned_url")] public List<string> PartsPresignedUrl { get; set; } = new();
        [JsonPropertyName("complete_presigned_url")] public string CompletePresignedUrl { get; set; } = "";

        public string SessionUuid => string.IsNullOrWhiteSpace(Uuid) ? Id : Uuid;
    }

    public class CollectionUploadResult
    {
        public string CollectionId { get; set; } = "";
        public string Slug { get; set; } = "";
        public string RevisionId { get; set; } = "";
        public int RevisionNumber { get; set; }
        public bool Success { get; set; }
    }

    public class ProgressStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _totalLength;
        private readonly Action<long, long> _progressCallback;
        private long _bytesRead;
        private DateTime _lastReport = DateTime.MinValue;

        public ProgressStream(Stream baseStream, long totalLength, Action<long, long> progressCallback)
        {
            _baseStream = baseStream;
            _totalLength = totalLength;
            _progressCallback = progressCallback;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = _baseStream.Read(buffer, offset, count);
            _bytesRead += n;
            if ((DateTime.Now - _lastReport).TotalMilliseconds > 100)
            { _progressCallback(_bytesRead, _totalLength); _lastReport = DateTime.Now; }
            return n;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var n = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            _bytesRead += n;
            if ((DateTime.Now - _lastReport).TotalMilliseconds > 100)
            { _progressCallback(_bytesRead, _totalLength); _lastReport = DateTime.Now; }
            return n;
        }

        public override void Flush()
            => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin)
            => _baseStream.Seek(offset, origin);
        public override void SetLength(long value)
            => _baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _progressCallback(_bytesRead, _totalLength);
            base.Dispose(disposing);
        }
    }
}