using Microsoft.Extensions.Logging;
using System;
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
            public string type { get; set; } = "nexus";

            // nexus-type fields only
            public long modId { get; set; }
            public long fileId { get; set; }

            // off-site/browse-type fields
            public string? url { get; set; }

            // Common field
            public string? updatePolicy { get; set; }
        }

        private sealed class ManifestMod
        {
            public string name { get; set; } = "Unknown";
            public string version { get; set; } = "1.0.0";
            public bool optional { get; set; }
            public string domainName { get; set; } = "";
            public ManifestModSource source { get; set; } = new();
        }

        private readonly ILogger _logger;
        private readonly ITokenProvider<NexusOAuthState> _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        public delegate void UploadProgressHandler(string stage, double progress);
        public event UploadProgressHandler? OnProgress;

        // via nexusmods/nexus-api talks to https://api.nexusmods.com/v2/graphql
        private static readonly string GraphQLUrl =
            Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL")
            ?? "https://api.nexusmods.com/v2/graphql";

        private const int CollectionSchemaId = 2;

        // This cap prevents hammering the API
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
    int? existingCollectionId = null,
    string? gameVersion = null,
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

                // attempt new url so a retry never reuses a stale URL.
                string? assetFileUUID = null;
                bool uploadSuccess = false;

                for (int uploadAttempt = 1; uploadAttempt <= MaxUploadAttempts; uploadAttempt++)
                {
                    OnProgress?.Invoke("requesting_url", 0.0);
                    _logger.LogInformation(
                        "Requesting upload URL from Nexus Mods (attempt {attempt}/{max})...",
                        uploadAttempt, MaxUploadAttempts);

                    var uploadUrlResult = await GetRevisionUploadUrl(authState.OAuth.AccessToken, token);
                    if (uploadUrlResult == null)
                    {
                        _logger.LogError("Failed to get upload URL from Nexus on attempt {attempt}", uploadAttempt);
                        if (uploadAttempt < MaxUploadAttempts)
                        {
                            var delay = TimeSpan.FromSeconds(Math.Pow(2, uploadAttempt) * 5);
                            _logger.LogInformation("Waiting {delay}s before retrying URL request...", delay.TotalSeconds);
                            await Task.Delay(delay, token);
                            continue;
                        }
                        return null;
                    }

                    _logger.LogInformation("Got upload URL with UUID: {uuid}", uploadUrlResult.Uuid);

                    OnProgress?.Invoke("upload", 0.0);
                    _logger.LogInformation(
                        "Uploading collection archive ({size:N0} bytes, {mb:N0} MB)...",
                        archivePath.Size(), archivePath.Size() / (1024 * 1024));

                    uploadSuccess = await UploadFileToPresignedUrl(uploadUrlResult.Url, archivePath, uploadAttempt, MaxUploadAttempts, token);

                    if (uploadSuccess)
                    {
                        assetFileUUID = uploadUrlResult.Uuid;
                        break;
                    }

                    if (uploadAttempt < MaxUploadAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, uploadAttempt) * 5);
                        _logger.LogWarning(
                            "Upload attempt {attempt} failed. Fetching a fresh pre-signed URL and retrying in {delay}s...",
                            uploadAttempt, delay.TotalSeconds);
                        await Task.Delay(delay, token);
                    }
                }

                if (!uploadSuccess || assetFileUUID == null)
                {
                    _logger.LogError("File upload failed after {max} attempts", MaxUploadAttempts);
                    return null;
                }

                _logger.LogInformation("File uploaded successfully (UUID: {uuid})", assetFileUUID);

                OnProgress?.Invoke("building_manifest", 0.0);
                var collectionPayload = WabbajackToVortexCollection.Build(modList, gameVersion);

                OnProgress?.Invoke("sending_manifest", 0.0);
                _logger.LogInformation(
                    "Creating/updating collection on Nexus Mods (existingCollectionId={id})",
                    existingCollectionId.HasValue ? existingCollectionId.Value.ToString() : "none");

                CollectionUploadResult? result = null;

                if (existingCollectionId.HasValue && existingCollectionId.Value > 0)
                {
                    result = await CreateOrUpdateRevision(
                        collectionPayload,
                        assetFileUUID,
                        existingCollectionId.Value,
                        modList.IsNSFW,
                        authState.OAuth.AccessToken,
                        collectionJsonPath,
                        token);

                    if (result == null)
                    {
                        _logger.LogWarning(
                            "createOrUpdateRevision failed for collectionId={id}. Falling back to createCollection.",
                            existingCollectionId.Value);
                    }
                }

                if (result == null)
                {
                    result = await CreateCollection(
                        collectionPayload,
                        assetFileUUID,
                        modList.IsNSFW,
                        authState.OAuth.AccessToken,
                        collectionJsonPath,
                        token);
                }

                if (result != null && result.Success)
                {
                    _logger.LogInformation(
                        "Collection uploaded successfully! Slug: {slug}, Revision: {revision}",
                        result.Slug, result.RevisionNumber);

                    OnProgress?.Invoke("finalizing", 0.0);
                    await UpdateCollectionCategory(result.CollectionId, "wabbajack", authState.OAuth.AccessToken, token);

                    // Tag id 25 == wabbajack
                    var tagAdded = await AddCollectionTag(result.CollectionId, 25, authState.OAuth.AccessToken, token);
                    if (!tagAdded)
                        _logger.LogInformation("Wabbajack tag already present on collection");

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

        private async Task<bool> UpdateCollectionCategory(
            int collectionId,
            string category,
            string accessToken,
            CancellationToken token)
        {
            var mutation = @"
mutation updateCollection($collectionId: Int!, $category: String!) {
  updateCollection(collectionId: $collectionId, category: $category) {
    success
  }
}";

            var variables = new { collectionId, category };
            var graphqlRequest = new { query = mutation, variables };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update collection category: {status} - {body}",
                    response.StatusCode, responseBody);
                return false;
            }

            _logger.LogInformation("Collection category updated to '{category}'", category);
            return true;
        }

        private async Task<bool> AddCollectionTag(
            int collectionId,
            int tagId,
            string accessToken,
            CancellationToken token)
        {
            var mutation = @"
mutation addTagToCollection($collectionId: Int!, $tagIds: [ID!]!) {
  addTagToCollection(collectionId: $collectionId, tagIds: $tagIds) {
    success
  }
}";

            var variables = new { collectionId, tagIds = new[] { tagId } };
            var graphqlRequest = new { query = mutation, variables };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to add tag to collection: {status} - {body}",
                    response.StatusCode, responseBody);
                return false;
            }

            try
            {
                var root = JsonNode.Parse(responseBody) as JsonObject;
                var errors = root?["errors"] as JsonArray;
                if (errors is { Count: > 0 })
                {
                    var firstError = errors[0] as JsonObject;
                    var code = firstError?["extensions"]?["code"]?.GetValue<string>();
                    if (code == "TAG_ALREADY_ATTACHED")
                    {
                        _logger.LogInformation("Tag {tagId} already attached to collection {id}", tagId, collectionId);
                        return true;
                    }

                    _logger.LogError("GraphQL returned errors while adding tag to collection: {errors}",
                        errors.ToJsonString());
                    return false;
                }

                var success = root?["data"]?["addTagToCollection"]?["success"]?.GetValue<bool>() ?? false;
                if (!success)
                {
                    _logger.LogWarning("addTagToCollection returned success=false for collection {id} and tag {tagId}",
                        collectionId, tagId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse GraphQL response for addTagToCollection");
                return false;
            }

            _logger.LogInformation("Added tag ID {tagId} to collection {id}", tagId, collectionId);
            return true;
        }

        private async Task<PreSignedUrlResult?> GetRevisionUploadUrl(string accessToken, CancellationToken token)
        {
            var query = @"
query collectionRevisionUploadUrl {
  collectionRevisionUploadUrl {
    url
    uuid
  }
}";

            var graphqlRequest = new { query };

            var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GraphQL request failed: {status} - {body}", response.StatusCode, responseBody);
                return null;
            }

            var result = JsonSerializer.Deserialize<GraphQLResponse<PreSignedUrlResponse>>(responseBody, _jsonOptions);

            if (result?.Errors?.Length > 0)
            {
                _logger.LogError("GraphQL returned errors while requesting upload URL: {errors}",
                    JsonSerializer.Serialize(result.Errors, _jsonOptions));
                return null;
            }

            return result?.Data?.CollectionRevisionUploadUrl;
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

            return new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }

        private async Task<bool> UploadFileToPresignedUrl(
            string presignedUrl,
            AbsolutePath filePath,
            int currentAttempt,
            int maxAttempts,
            CancellationToken token)
        {
            try
            {
                var fileSize = filePath.Size();
                _logger.LogInformation(
                    "Starting upload to pre-signed URL (attempt {attempt}/{max}, {size:N0} bytes / {mb:N0} MB)...",
                    currentAttempt, maxAttempts, fileSize, fileSize / (1024 * 1024));

                var estimatedSeconds = (fileSize / (625 * 1024)) + 300;
                var uploadTimeout = TimeSpan.FromSeconds(Math.Clamp(estimatedSeconds, 45 * 60, 4 * 60 * 60));
                _logger.LogInformation(
                    "Upload timeout set to {minutes:N0} minutes based on file size.",
                    uploadTimeout.TotalMinutes);

                await using var fileStream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

                var progressStream = new ProgressStream(fileStream, fileSize, (bytesRead, totalBytes) =>
                {
                    var percentage = (double)bytesRead / totalBytes;
                    OnProgress?.Invoke("upload", percentage);
                });

                using var content = new StreamContent(progressStream, 256 * 1024);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = fileSize;

                using var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl) { Content = content };

                using var uploadClient = CreateUploadClient();
                using var uploadCts = new CancellationTokenSource(uploadTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, uploadCts.Token);

                using var response = await uploadClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Upload completed successfully (HTTP {status})", (int)response.StatusCode);
                    return true;
                }

                string errorBody;
                try
                {
                    using var errorCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    errorBody = await response.Content.ReadAsStringAsync(errorCts.Token);
                    if (errorBody.Length > 2000) errorBody = errorBody[..2000] + "…";
                }
                catch
                {
                    errorBody = "(could not read error body)";
                }

                _logger.LogError("Upload failed: HTTP {status} - {body}", response.StatusCode, errorBody);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex,
                    "Upload timed out or was cancelled on attempt {attempt}/{max}",
                    currentAttempt, maxAttempts);
                return false;
            }
            catch (HttpRequestException ex) when (
                ex.InnerException is IOException ioEx &&
                ioEx.InnerException is System.Net.Sockets.SocketException sockEx)
            {
                _logger.LogWarning(
                    "Network error on attempt {attempt}/{max}: {socketError} — {message}",
                    currentAttempt, maxAttempts, sockEx.SocketErrorCode, ex.Message);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "HTTP error on attempt {attempt}/{max}", currentAttempt, maxAttempts);
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex,
                    "IO error on attempt {attempt}/{max}", currentAttempt, maxAttempts);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during upload attempt {attempt}/{max}", currentAttempt, maxAttempts);
                return false;
            }
        }

        private async Task<CollectionUploadResult?> CreateCollection(
            WabbajackToVortexCollection.VortexCollection collectionPayload,
            string assetFileUUID,
            bool adultContent,
            string accessToken,
            AbsolutePath collectionJsonPath,
            CancellationToken token)
        {
            var mutation = @"
mutation createCollection($collectionData: CollectionPayload!, $uuid: String!) {
  createCollection(collectionData: $collectionData, uuid: $uuid) {
    collection { id slug }
    revision { id revisionNumber }
    success
  }
}";

            return await ExecuteCollectionMutation(
                mutation,
                "createCollection",
                collectionPayload,
                assetFileUUID,
                null,
                adultContent,
                accessToken,
                collectionJsonPath,
                token);
        }

        private async Task<CollectionUploadResult?> CreateOrUpdateRevision(
            WabbajackToVortexCollection.VortexCollection collectionPayload,
            string assetFileUUID,
            int collectionId,
            bool adultContent,
            string accessToken,
            AbsolutePath collectionJsonPath,
            CancellationToken token)
        {
            var mutation = @"
mutation createOrUpdateRevision($collectionData: CollectionPayload!, $uuid: String!, $collectionId: Int!) {
  createOrUpdateRevision(collectionData: $collectionData, uuid: $uuid, collectionId: $collectionId) {
    collection { id slug }
    revision { id revisionNumber }
    success
  }
}";

            return await ExecuteCollectionMutation(
                mutation,
                "createOrUpdateRevision",
                collectionPayload,
                assetFileUUID,
                collectionId,
                adultContent,
                accessToken,
                collectionJsonPath,
                token);
        }

        private async Task<CollectionUploadResult?> ExecuteCollectionMutation(
            string mutation,
            string operationName,
            WabbajackToVortexCollection.VortexCollection collectionPayload,
            string assetFileUUID,
            int? collectionId,
            bool adultContent,
            string accessToken,
            AbsolutePath collectionJsonPath,
            CancellationToken token)
        {
            // not send installInstructions it's not needed and bloats the payload
            var manifestInfo = new
            {
                author = string.IsNullOrWhiteSpace(collectionPayload.Info.Author) ? "Anonymous" : collectionPayload.Info.Author,
                authorUrl = string.IsNullOrWhiteSpace(collectionPayload.Info.AuthorUrl) ? null : collectionPayload.Info.AuthorUrl,
                name = string.IsNullOrWhiteSpace(collectionPayload.Info.Name) ? "Wabbajack Collection" : collectionPayload.Info.Name,
                summary = string.IsNullOrWhiteSpace(collectionPayload.Info.Summary) ? null : collectionPayload.Info.Summary.Trim(),
                description = string.IsNullOrWhiteSpace(collectionPayload.Info.Description) ? null : collectionPayload.Info.Description,
                domainName = collectionPayload.Info.DomainName,
                gameVersions = (collectionPayload.Info.GameVersions?.Count ?? 0) == 0 ? null : collectionPayload.Info.GameVersions,
            };

            _logger.LogInformation(
                "Manifest info - author: '{author}', name: '{name}', summary: '{summary}', descLen: {descLen}, gameVersions: {gameVersions}",
                manifestInfo.author,
                manifestInfo.name,
                manifestInfo.summary,
                manifestInfo.description?.Length ?? 0,
                manifestInfo.gameVersions != null ? string.Join(", ", manifestInfo.gameVersions) : "null");

            const int MinCollectionNameLength = 3;
            const int MaxCollectionNameLength = 36;

            if (manifestInfo.name.Length < MinCollectionNameLength)
            {
                _logger.LogError("Collection name too short for Nexus (min {min}): '{name}'",
                    MinCollectionNameLength, manifestInfo.name);
                return null;
            }

            object infoToSend = manifestInfo;
            if (manifestInfo.name.Length > MaxCollectionNameLength)
            {
                _logger.LogWarning("Collection name too long for Nexus (max {max}), truncating: '{name}'",
                    MaxCollectionNameLength, manifestInfo.name);
                infoToSend = new
                {
                    manifestInfo.author,
                    manifestInfo.authorUrl,
                    name = manifestInfo.name.Substring(0, MaxCollectionNameLength),
                    manifestInfo.summary,
                    manifestInfo.description,
                    manifestInfo.domainName,
                    manifestInfo.gameVersions,
                };
            }

            if (string.IsNullOrWhiteSpace(manifestInfo.domainName) || manifestInfo.domainName == "site")
            {
                _logger.LogError(
                    "Invalid/unknown Nexus domainName for this modlist: '{domain}'. Must be a real game domain (e.g. skyrimspecialedition).",
                    manifestInfo.domainName);
                return null;
            }

            var manifestMods = (collectionPayload.Mods ?? new List<WabbajackToVortexCollection.VortexMod>())
                .Select(mod => new ManifestMod
                {
                    name = string.IsNullOrWhiteSpace(mod.Name) ? "Unknown" : mod.Name,
                    version = string.IsNullOrWhiteSpace(mod.Version) ? "1.0.0" : mod.Version,
                    optional = mod.Optional,
                    domainName = string.IsNullOrWhiteSpace(mod.DomainName) ? manifestInfo.domainName : mod.DomainName,
                    source = new ManifestModSource
                    {
                        type = mod.Source.Type,
                        modId = mod.Source.ModId,
                        fileId = mod.Source.FileId,
                        updatePolicy = "prefer",
                        url = mod.Source.Url,
                    },
                })
                .ToList();

            if (manifestMods.Count == 0)
            {
                _logger.LogError("Cannot create a Nexus collection with 0 mods.");
                return null;
            }

            var invalidNexusMods = manifestMods
                .Where(m => m.source.type == "nexus" && (m.source.modId <= 0 || m.source.fileId <= 0))
                .Take(5)
                .ToList();

            if (invalidNexusMods.Count > 0)
            {
                _logger.LogError("Some mods are missing Nexus modId/fileId. First few: {mods}",
                    JsonSerializer.Serialize(
                        invalidNexusMods.Select(m => new { m.name, m.source.modId, m.source.fileId }),
                        _jsonOptions));
                return null;
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var variables = new
                {
                    collectionData = new
                    {
                        adultContent,
                        collectionSchemaId = CollectionSchemaId,
                        collectionManifest = new
                        {
                            info = infoToSend,
                            mods = manifestMods,
                        },
                    },
                    uuid = assetFileUUID,
                    collectionId
                };

                var graphqlRequest = new { query = mutation, variables };

                var serializedRequest = JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                // Save the actual GraphQL payload for debugging
                var debugPath = collectionJsonPath.Parent.Combine(
                    $"{collectionJsonPath.FileName}_graphql_payload.json");
                await debugPath.WriteAllTextAsync(serializedRequest);
                _logger.LogInformation("Saved GraphQL payload to {path} ({kb} KB, {count} mods)",
                    debugPath, serializedRequest.Length / 1024, manifestMods.Count);

                using var content = new StringContent(serializedRequest, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
                AddNexusHeaders(request, accessToken);

                using var mutationCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, mutationCts.Token);

                using var mutationClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

                string responseBody;

                try
                {
                    var response = await mutationClient.SendAsync(request, linkedCts.Token);
                    responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("GraphQL mutation failed: {status} - {body}",
                            response.StatusCode, responseBody);

                        if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                        {
                            _logger.LogInformation(
                                "Gateway timeout on manifest attempt {attempt} — checking if collection was created...",
                                attempt);

                            await Task.Delay(TimeSpan.FromSeconds(15), token);

                            var existingCollection = await FindRecentlyCreatedCollection(
                                collectionPayload.Info.Name,
                                collectionPayload.Info.DomainName,
                                accessToken,
                                token);

                            if (existingCollection != null)
                            {
                                _logger.LogWarning(
                                    "Collection was created despite timeout! Using collection ID {id}",
                                    existingCollection.CollectionId);
                                return existingCollection;
                            }

                            if (attempt < 3)
                            {
                                _logger.LogWarning("Collection not found yet, retrying in 20s (attempt {next}/3)...", attempt + 1);
                                await Task.Delay(TimeSpan.FromSeconds(20), token);
                                continue;
                            }

                            _logger.LogError("Collection creation failed after 3 manifest attempts with timeouts");
                            return null;
                        }

                        return null;
                    }
                }
                catch (TaskCanceledException) when (attempt < 3)
                {
                    _logger.LogInformation(
                        "Manifest request cancelled/timed-out on attempt {attempt} — checking if collection was created...",
                        attempt);
                    await Task.Delay(TimeSpan.FromSeconds(15), token);

                    var existingCollection = await FindRecentlyCreatedCollection(
                        collectionPayload.Info.Name,
                        collectionPayload.Info.DomainName,
                        accessToken,
                        token);

                    if (existingCollection != null)
                    {
                        _logger.LogWarning(
                            "Collection was created despite cancellation! Using collection ID {id}",
                            existingCollection.CollectionId);
                        return existingCollection;
                    }

                    _logger.LogWarning("Manifest attempt {attempt} timed out, retrying in 20s...", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(20), token);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manifest attempt {attempt}", attempt);
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(20), token);
                        continue;
                    }
                    throw;
                }

                // Check for GraphQL-level errors and try to remove invalid mods
                try
                {
                    var root = JsonNode.Parse(responseBody) as JsonObject;
                    var errors = root?["errors"] as JsonArray;
                    if (errors is { Count: > 0 })
                    {
                        _logger.LogError("GraphQL returned errors during {op}: {errors}",
                            operationName, errors.ToJsonString());

                        var removedAny = TryRemoveInvalidModsFromErrors(errors, ref manifestMods);
                        if (removedAny)
                        {
                            if (manifestMods.Count == 0)
                            {
                                _logger.LogError("After removing invalid mods, 0 mods remain. Aborting.");
                                return null;
                            }

                            _logger.LogWarning(
                                "Retrying {op} after removing invalid mods. Remaining: {count}",
                                operationName, manifestMods.Count);
                            continue;
                        }

                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse GraphQL error payload; falling back to typed parse.");
                }

                var result = JsonSerializer.Deserialize<GraphQLMutationResponse>(responseBody, _jsonOptions);
                if (result?.Data == null)
                {
                    _logger.LogError("No data in GraphQL response");
                    return null;
                }

                var mutationResult = operationName switch
                {
                    "createCollection" => result.Data.CreateCollection,
                    "createOrUpdateRevision" => result.Data.CreateOrUpdateRevision,
                    _ => null
                };

                if (mutationResult == null)
                {
                    _logger.LogError("Mutation result not found in response for operation '{op}'", operationName);
                    return null;
                }

                _logger.LogInformation("Collection mutation '{op}' succeeded on attempt {attempt}!", operationName, attempt);

                return new CollectionUploadResult
                {
                    CollectionId = mutationResult.Collection.Id,
                    Slug = mutationResult.Collection.Slug,
                    RevisionId = mutationResult.Revision.Id,
                    RevisionNumber = mutationResult.Revision.RevisionNumber,
                    Success = mutationResult.Success
                };
            }

            _logger.LogError("Exceeded retry attempts for {op}", operationName);
            return null;
        }

        private static void AddNexusHeaders(HttpRequestMessage request, string accessToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
            request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
            request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");
        }

        private bool TryRemoveInvalidModsFromErrors(JsonArray errors, ref List<ManifestMod> manifestMods)
        {
            var badModIds = new HashSet<long>();
            var badFileIds = new HashSet<long>();

            foreach (var errNode in errors)
            {
                var ext = errNode?["extensions"] as JsonObject;
                var detail = ext?["detail"] as JsonArray;
                if (detail == null) continue;

                foreach (var detNode in detail)
                {
                    var attribute = detNode?["attribute"]?.GetValue<string>();
                    var code = detNode?["code"]?.GetValue<string>();
                    var valueNode = detNode?["value"];

                    if (valueNode == null) continue;
                    if (code is not ("NOT_FOUND" or "NOT_AVAILABLE" or "DELETED")) continue;

                    if (attribute == "modId" && TryGetLong(valueNode, out var modId))
                        badModIds.Add(modId);
                    else if (attribute == "fileId" && TryGetLong(valueNode, out var fileId))
                        badFileIds.Add(fileId);
                }
            }

            if (badModIds.Count == 0 && badFileIds.Count == 0)
                return false;

            var before = manifestMods.Count;
            manifestMods = manifestMods
                .Where(m => !badModIds.Contains(m.source.modId) && !badFileIds.Contains(m.source.fileId))
                .ToList();

            var removed = before - manifestMods.Count;
            if (removed > 0)
            {
                _logger.LogWarning(
                    "Removed {count} invalid mod entries (bad modIds: {modIds}; bad fileIds: {fileIds})",
                    removed,
                    string.Join(",", badModIds),
                    string.Join(",", badFileIds));
            }

            return removed > 0;
        }

        private static bool TryGetLong(JsonNode node, out long value)
        {
            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<long>(out value)) return true;
                if (jv.TryGetValue<int>(out var i)) { value = i; return true; }
                if (jv.TryGetValue<string>(out var s) && long.TryParse(s, out value)) return true;
            }

            value = default;
            return false;
        }

        private async Task<CollectionUploadResult?> FindRecentlyCreatedCollection(
            string collectionName,
            string domainName,
            string accessToken,
            CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Searching for collection '{name}' in domain '{domain}'...",
                    collectionName, domainName);

                var targetName = collectionName.Length > 36 ? collectionName[..36] : collectionName;

                var deadline = DateTime.UtcNow.AddSeconds(90);
                var attempt = 0;

                while (DateTime.UtcNow < deadline)
                {
                    attempt++;

                    var found = await FindRecentlyCreatedCollection_MyCollectionsSafeList(
                        targetName, domainName, accessToken, token);

                    if (found != null)
                        return found;

                    var delay = TimeSpan.FromSeconds(Math.Min(5 + (attempt * 2), 15));
                    _logger.LogInformation(
                        "Collection not visible yet; re-checking in {seconds}s (attempt {attempt})",
                        delay.TotalSeconds, attempt);
                    await Task.Delay(delay, token);
                }

                _logger.LogWarning(
                    "No collection found matching '{name}' in domain '{domain}' within 90s window",
                    targetName, domainName);

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
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
            CancellationToken token)
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
                      publishedAt
                    }
                  }
                }";

            var variables = new
            {
                count = 50,
                offset = 0,
                viewAdultContent = true,
                viewUnlisted = true,
                viewUnderModeration = true
            };

            var graphqlRequest = new { query, variables };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl) { Content = content };
            AddNexusHeaders(request, accessToken);

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("myCollections request failed: {status} - {body}",
                    response.StatusCode, responseBody);
                return null;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(responseBody) as JsonObject;
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "myCollections returned unparseable JSON");
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

            _logger.LogDebug("myCollections returned {count} nodes; scanning for match", nodes.Count);

            foreach (var node in nodes)
            {
                if (node is not JsonObject obj)
                    continue;

                var name = obj["name"]?.GetValue<string>();
                var gameDomain = obj["game"]?["domainName"]?.GetValue<string>();
                var slug = obj["slug"]?.GetValue<string>();
                var collectionIdValue = ParseIntId(obj["id"]);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(gameDomain))
                    continue;

                if (!name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!gameDomain.Equals(domainName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var draftRev = obj["draftRevisionNumber"]?.GetValue<int?>() ?? 1;

                _logger.LogInformation(
                    "Found matching collection: id={id} slug='{slug}' draftRev={draftRev}",
                    collectionIdValue, slug, draftRev);

                return new CollectionUploadResult
                {
                    CollectionId = collectionIdValue,
                    Slug = slug ?? "",
                    RevisionId = 0,
                    RevisionNumber = draftRev,
                    Success = true
                };
            }

            return null;
        }

        private static int ParseIntId(JsonNode? idNode)
        {
            try
            {
                if (idNode == null) return 0;

                if (idNode is JsonValue jv)
                {
                    if (jv.TryGetValue<int>(out var i)) return i;
                    if (jv.TryGetValue<long>(out var l))
                    {
                        if (l > int.MaxValue) return 0;
                        return (int)l;
                    }
                    if (jv.TryGetValue<string>(out var s))
                    {
                        if (int.TryParse(s, out var parsedInt)) return parsedInt;
                        if (long.TryParse(s, out var parsedLong))
                        {
                            if (parsedLong > int.MaxValue) return 0;
                            return (int)parsedLong;
                        }
                    }
                }
            }
            catch { /* ignored */ }

            return 0;
        }
    }
    public class PreSignedUrlResult
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";
    }

    public class PreSignedUrlResponse
    {
        [JsonPropertyName("collectionRevisionUploadUrl")]
        public PreSignedUrlResult CollectionRevisionUploadUrl { get; set; } = new();
    }

    public class GraphQLResponse<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("errors")]
        public object[]? Errors { get; set; }
    }

    public class GraphQLMutationResponse
    {
        [JsonPropertyName("data")]
        public MutationData? Data { get; set; }

        [JsonPropertyName("errors")]
        public object[]? Errors { get; set; }
    }

    public class MutationData
    {
        [JsonPropertyName("createCollection")]
        public CreateCollectionResult? CreateCollection { get; set; }

        [JsonPropertyName("createOrUpdateRevision")]
        public CreateCollectionResult? CreateOrUpdateRevision { get; set; }
    }

    public class CreateCollectionResult
    {
        [JsonPropertyName("collection")]
        public CollectionInfo Collection { get; set; } = new();

        [JsonPropertyName("revision")]
        public RevisionInfo Revision { get; set; } = new();

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class CollectionInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";
    }

    public class RevisionInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("revisionNumber")]
        public int RevisionNumber { get; set; }
    }

    public class CollectionUploadResult
    {
        public int CollectionId { get; set; }
        public string Slug { get; set; } = "";
        public int RevisionId { get; set; }
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
            var bytesRead = _baseStream.Read(buffer, offset, count);
            _bytesRead += bytesRead;

            if ((DateTime.Now - _lastReport).TotalMilliseconds > 100)
            {
                _progressCallback(_bytesRead, _totalLength);
                _lastReport = DateTime.Now;
            }

            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            _bytesRead += bytesRead;

            if ((DateTime.Now - _lastReport).TotalMilliseconds > 100)
            {
                _progressCallback(_bytesRead, _totalLength);
                _lastReport = DateTime.Now;
            }

            return bytesRead;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _progressCallback(_bytesRead, _totalLength);
            base.Dispose(disposing);
        }
    }
}