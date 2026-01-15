using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using FileMode = System.IO.FileMode;

namespace Wabbajack.Compiler
{
    public class NexusCollectionUploader
    {
        private sealed class ManifestModSource
        {
            public string type { get; set; } = "nexus";
            public long modId { get; set; }
            public long fileId { get; set; }
            public string? updatePolicy { get; set; }
            public string? logicalFilename { get; set; }
        }

        private sealed class ManifestMod
        {
            public string name { get; set; } = "Unknown";
            public string version { get; set; } = "1.0.0";
            public bool optional { get; set; }
            public string domainName { get; set; } = "";
            public string? author { get; set; }
            public ManifestModSource source { get; set; } = new();
        }

        private readonly ILogger _logger;
        private readonly ITokenProvider<NexusOAuthState> _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        // via nexusmods/nexus-api talks to https://api.nexusmods.com/v2/graphql
        private static readonly string GraphQLUrl =
            Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL")
            ?? "https://api.nexusmods.com/v2/graphql";

        // same as Vortex
        private const int CollectionSchemaId = 1;

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
            CancellationToken token = default)
        {
            try
            {
                // Verify we have OAuth credentials
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

                _logger.LogInformation("Requesting upload URL from Nexus Mods...");
                var uploadUrlResult = await GetRevisionUploadUrl(authState.OAuth.AccessToken, token);
                if (uploadUrlResult == null)
                {
                    _logger.LogError("Failed to get upload URL from Nexus");
                    return null;
                }

                _logger.LogInformation("Got upload URL with UUID: {uuid}", uploadUrlResult.Uuid);

                _logger.LogInformation("Uploading collection archive ({size:N0} bytes)...", archivePath.Size());
                var uploadSuccess = await UploadFileToPresignedUrl(uploadUrlResult.Url, archivePath, token);
                if (!uploadSuccess)
                {
                    _logger.LogError("Failed to upload file to pre-signed URL");
                    return null;
                }

                _logger.LogInformation("File uploaded successfully");

                var collectionPayload = WabbajackToVortexCollection.Build(modList);

                _logger.LogInformation("Creating/updating collection on Nexus Mods...");
                CollectionUploadResult? result;
                if (existingCollectionId.HasValue)
                {
                    result = await CreateOrUpdateRevision(
                        collectionPayload,
                        uploadUrlResult.Uuid,
                        existingCollectionId.Value,
                        modList.IsNSFW,
                        authState.OAuth.AccessToken,
                        token);
                }
                else
                {
                    result = await CreateCollection(
                        collectionPayload,
                        uploadUrlResult.Uuid,
                        modList.IsNSFW,
                        authState.OAuth.AccessToken,
                        token);
                }

                if (result != null && result.Success)
                {
                    _logger.LogInformation(
                        "Collection uploaded successfully! Slug: {slug}, Revision: {revision}",
                        result.Slug,
                        result.RevisionNumber);
                    await UpdateCollectionCategory(result.CollectionId, "wabbajack", authState.OAuth.AccessToken, token);
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

            var variables = new
            {
                collectionId,
                category
            };

            var graphqlRequest = new
            {
                query = mutation,
                variables
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
            request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
            request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

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

        private async Task<PreSignedUrlResult?> GetRevisionUploadUrl(
            string accessToken,
            CancellationToken token)
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

            var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
            request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
            request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GraphQL request failed: {status} - {body}", response.StatusCode, responseBody);
                return null;
            }

            var result = JsonSerializer.Deserialize<GraphQLResponse<PreSignedUrlResponse>>(
                responseBody, _jsonOptions);

            if (result?.Errors?.Length > 0)
            {
                _logger.LogError("GraphQL returned errors while requesting upload URL: {errors}",
                    JsonSerializer.Serialize(result.Errors, _jsonOptions));
                return null;
            }

            return result?.Data?.CollectionRevisionUploadUrl;
        }

        private async Task<bool> UploadFileToPresignedUrl(
            string presignedUrl,
            AbsolutePath filePath,
            CancellationToken token)
        {
            try
            {
                var fileSize = filePath.Size();

                _logger.LogInformation("Uploading to pre-signed URL...");
                await using var stream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = fileSize;

                using var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl)
                {
                    Content = content,
                };

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(token);
                    _logger.LogError("Upload failed: {status} - {body}", response.StatusCode, errorBody);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload to pre-signed URL");
                return false;
            }
        }

        private async Task<CollectionUploadResult?> CreateCollection(
            WabbajackToVortexCollection.VortexCollection collectionPayload,
            string assetFileUUID,
            bool adultContent,
            string accessToken,
            CancellationToken token)
        {
            var mutation = @"
                mutation createCollection($collectionData: CollectionPayload!, $uuid: String!) {
                    createCollection(collectionData: $collectionData, uuid: $uuid) {
                        collection {
                            id
                            slug
                        }
                        revision {
                            id
                            revisionNumber
                        }
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
                token);
        }

        private async Task<CollectionUploadResult?> CreateOrUpdateRevision(
            WabbajackToVortexCollection.VortexCollection collectionPayload,
            string assetFileUUID,
            int collectionId,
            bool adultContent,
            string accessToken,
            CancellationToken token)
        {
            var mutation = @"
                mutation createOrUpdateRevision($collectionData: CollectionPayload!, $uuid: String!, $collectionId: Int!) {
                    createOrUpdateRevision(collectionData: $collectionData, uuid: $uuid, collectionId: $collectionId) {
                        collection {
                            id
                            slug
                        }
                        revision {
                            id
                            revisionNumber
                        }
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
            CancellationToken token)
        {
            // 
            // The Vortex collection.json format contains fields that are not accepted by the
            // Nexus GraphQL CollectionManifest
            //
            // In Vortex this is handled by filtering before upload:
            // info: omit installInstructions
            // mods: omit hashes/choices/patches/details/instructions/phase/fileOverrides
            // source: omit instructions/fileSize/tag

            // CollectionManifestInfo does not include installInstructions.
            // treat optional fields as null when empty so they are omitted from the payload.
            var manifestInfo = new
            {
                author = string.IsNullOrWhiteSpace(collectionPayload.Info.Author)
                  ? "Anonymous"
                  : collectionPayload.Info.Author,
                authorUrl = string.IsNullOrWhiteSpace(collectionPayload.Info.AuthorUrl)
                  ? null
                  : collectionPayload.Info.AuthorUrl,
                name = string.IsNullOrWhiteSpace(collectionPayload.Info.Name)
                  ? "Wabbajack Collection"
                  : collectionPayload.Info.Name,
                description = string.IsNullOrWhiteSpace(collectionPayload.Info.Description)
                  ? null
                  : collectionPayload.Info.Description,
                domainName = collectionPayload.Info.DomainName,
                gameVersions = (collectionPayload.Info.GameVersions?.Count ?? 0) == 0
                  ? null
                  : collectionPayload.Info.GameVersions,
            };

            // Vortex enforces name length in the UI Wabbajack doesn't,
            const int MinCollectionNameLength = 3;
            const int MaxCollectionNameLength = 36;
            if (manifestInfo.name.Length < MinCollectionNameLength)
            {
                _logger.LogError("Collection name too short for Nexus (min {min}): '{name}'", MinCollectionNameLength, manifestInfo.name);
                return null;
            }
            if (manifestInfo.name.Length > MaxCollectionNameLength)
            {
                _logger.LogWarning("Collection name too long for Nexus (max {max}), truncating: '{name}'", MaxCollectionNameLength, manifestInfo.name);
                manifestInfo = new
                {
                    manifestInfo.author,
                    manifestInfo.authorUrl,
                    name = manifestInfo.name.Substring(0, MaxCollectionNameLength),
                    manifestInfo.description,
                    manifestInfo.domainName,
                    manifestInfo.gameVersions,
                };
            }

            if (string.IsNullOrWhiteSpace(manifestInfo.domainName) || manifestInfo.domainName == "site")
            {
                _logger.LogError("Invalid/unknown Nexus domainName for this modlist: '{domain}'. This must be a real game domain (e.g. skyrimspecialedition).", manifestInfo.domainName);
                return null;
            }

            // CollectionManifestMod does not include details/phase/
            // CollectionManifestModSource does not include fileSize/instructions/tag
            var manifestMods = (collectionPayload.Mods ?? new List<WabbajackToVortexCollection.VortexMod>())
                .Select(mod => new ManifestMod
                {
                    name = string.IsNullOrWhiteSpace(mod.Name) ? "Unknown" : mod.Name,
                    version = string.IsNullOrWhiteSpace(mod.Version) ? "1.0.0" : mod.Version,
                    optional = mod.Optional,
                    domainName = string.IsNullOrWhiteSpace(mod.DomainName) ? manifestInfo.domainName : mod.DomainName,
                    author = string.IsNullOrWhiteSpace(mod.Author) ? null : mod.Author,
                    source = new ManifestModSource
                    {
                        type = mod.Source.Type,
                        modId = mod.Source.ModId,
                        fileId = mod.Source.FileId,
                        updatePolicy = mod.Source.UpdatePolicy,
                        logicalFilename = string.IsNullOrWhiteSpace(mod.Source.LogicalFilename) ? null : mod.Source.LogicalFilename,
                    },
                })
                .ToList();

            if (manifestMods.Count == 0)
            {
                _logger.LogError("Cannot create a Nexus collection with 0 mods. Ensure your modlist has Nexus-sourced archives.");
                return null;
            }

            var invalidNexusMods = manifestMods
                .Where(m => (m.source.type == "nexus")
                         && (m.source.modId <= 0 || m.source.fileId <= 0))
                .Take(5)
                .ToList();
            if (invalidNexusMods.Count > 0)
            {
                _logger.LogError("Some mods are missing Nexus modId/fileId. First few: {mods}",
                    JsonSerializer.Serialize(invalidNexusMods.Select(m => new { m.name, m.source.modId, m.source.fileId }), _jsonOptions));
                return null;
            }

            // try a couple of times. If Nexus rejects specific modIds/fileIds as NOT_FOUND/NOT_AVAILABLE,
            //  remove those entries and retry. This is important for Wabbajack modlists which may include
            // external tools (MO2, Bethini, etc.) that are not valid Nexus "mods" for the target game domain.
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var variables = new
                {
                    collectionData = new
                    {
                        // This must match nexusmods/nexus-api's ICollectionPayload shape
                        // (adultContent + collectionSchemaId + collectionManifest { info, mods })
                        adultContent,
                        collectionSchemaId = CollectionSchemaId,
                        collectionManifest = new
                        {
                            info = manifestInfo,
                            mods = manifestMods,
                        },
                    },
                    uuid = assetFileUUID,
                    collectionId = collectionId
                };

                var graphqlRequest = new
                {
                    query = mutation,
                    variables = variables
                };


                using var content = new StringContent(
                    JsonSerializer.Serialize(graphqlRequest, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    }),
                    Encoding.UTF8,
                    "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLUrl)
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
                request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
                request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

                var response = await _httpClient.SendAsync(request, token);
                var responseBody = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GraphQL mutation failed: {status} - {body}", response.StatusCode, responseBody);
                    return null;
                }

                // If there are errors, attempt to detect and remove invalid mod entries.
                try
                {
                    var root = JsonNode.Parse(responseBody) as JsonObject;
                    var errors = root?["errors"] as JsonArray;
                    if (errors is { Count: > 0 })
                    {
                        _logger.LogError("GraphQL returned errors during {op}: {errors}", operationName,
                            errors.ToJsonString());
                        _logger.LogError("GraphQL error response body during {op}: {body}", operationName, responseBody);

                        var removedAny = TryRemoveInvalidModsFromErrors(errors, ref manifestMods);
                        if (removedAny)
                        {
                            if (manifestMods.Count == 0)
                            {
                                _logger.LogError("After removing invalid Nexus mods, 0 mods remain. Aborting collection creation.");
                                return null;
                            }

                            _logger.LogWarning("Retrying {op} after removing invalid mod entries. Remaining mods: {count}",
                                operationName, manifestMods.Count);
                            continue;
                        }

                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse GraphQL error payload; will fall back to typed parsing.");
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
                    _logger.LogError("Mutation result not found in response");
                    return null;
                }

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
                    var entity = detNode?["entity"]?.GetValue<string>();
                    var attribute = detNode?["attribute"]?.GetValue<string>();
                    var code = detNode?["code"]?.GetValue<string>();
                    var valueNode = detNode?["value"];

                    if (valueNode == null) continue;
                    if (code is not ("NOT_FOUND" or "NOT_AVAILABLE" or "DELETED")) continue;

                    if (attribute == "modId" && TryGetLong(valueNode, out var modId))
                    {
                        badModIds.Add(modId);
                    }
                    else if (attribute == "fileId" && TryGetLong(valueNode, out var fileId))
                    {
                        badFileIds.Add(fileId);
                    }
                }
            }

            if (badModIds.Count == 0 && badFileIds.Count == 0)
            {
                return false;
            }

            var before = manifestMods.Count;
            manifestMods = manifestMods
                .Where(m => !badModIds.Contains(m.source.modId)
                         && !badFileIds.Contains(m.source.fileId))
                .ToList();

            var removed = before - manifestMods.Count;
            if (removed > 0)
            {
                _logger.LogWarning("Removed {count} invalid Nexus mod entries (bad modIds: {modIds}; bad fileIds: {fileIds})",
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

    }

    public class PreSignedUrlResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";
    }

    public class PreSignedUrlResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("collectionRevisionUploadUrl")]
        public PreSignedUrlResult CollectionRevisionUploadUrl { get; set; } = new();
    }

    public class GraphQLResponse<T>
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public T? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("errors")]
        public object[]? Errors { get; set; }
    }

    public class GraphQLMutationResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public MutationData? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("errors")]
        public object[]? Errors { get; set; }
    }

    public class MutationData
    {
        [System.Text.Json.Serialization.JsonPropertyName("createCollection")]
        public CreateCollectionResult? CreateCollection { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("createOrUpdateRevision")]
        public CreateCollectionResult? CreateOrUpdateRevision { get; set; }
    }

    public class CreateCollectionResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("collection")]
        public CollectionInfo Collection { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("revision")]
        public RevisionInfo Revision { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class CollectionInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("slug")]
        public string Slug { get; set; } = "";
    }

    public class RevisionInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("revisionNumber")]
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
}
