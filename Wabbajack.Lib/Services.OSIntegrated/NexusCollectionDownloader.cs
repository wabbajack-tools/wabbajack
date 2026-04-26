using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Services.OSIntegrated
{
    public class NexusCollectionDownloader
    {
        private readonly ILogger<NexusCollectionDownloader> _logger;
        private readonly ITokenProvider<NexusOAuthState> _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly GameLocator _gameLocator;

        private static readonly string GraphQLUrl =
            Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL")
            ?? "https://api.nexusmods.com/v2/graphql";

        // GraphQL query to get collection revision info with download link and game info
        private const string CollectionRevisionQuery = @"
            query collectionRevision($slug: String!, $revision: Int) {
                collectionRevision(slug: $slug, revision: $revision) {
                    id
                    revisionNumber
                    collectionId
                    downloadLink
                    collection {
                        id
                        slug
                        name
                        game {
                            id
                            name
                            domainName
                        }
                    }
                }
            }";

        public string? LastError { get; private set; }

        public NexusCollectionDownloader(
            ILogger<NexusCollectionDownloader> logger,
            ITokenProvider<NexusOAuthState> tokenProvider,
            HttpClient httpClient,
            GameLocator gameLocator)
        {
            _logger = logger;
            _tokenProvider = tokenProvider;
            _httpClient = httpClient;
            _gameLocator = gameLocator;
        }

        public async Task<CollectionDownloadInfo?> GetCollectionDownloadInfo(
            string collectionSlug,
            int? revisionNumber,
            CancellationToken token = default)
        {
            LastError = null;

            try
            {
                // Get OAuth token
                if (!_tokenProvider.HaveToken())
                {
                    _logger.LogError("No Nexus Mods OAuth token available for downloading collection");
                    LastError = "You are not logged in to Nexus Mods. Please log in to Nexus Mods in Wabbajack Settings to download collections.";
                    return null;
                }

                var authState = await _tokenProvider.Get();
                if (authState?.OAuth?.IsExpired ?? true)
                {
                    _logger.LogError("Nexus Mods OAuth token is expired");
                    LastError = "Your Nexus Mods login has expired. Please log in to Nexus Mods in Wabbajack Settings to refresh your authentication.";
                    return null;
                }

                _logger.LogInformation("Fetching collection revision info for {slug} (revision: {revision})",
                    collectionSlug, revisionNumber?.ToString() ?? "latest");

                // Query collection revision
                var variables = new
                {
                    slug = collectionSlug,
                    revision = revisionNumber
                };

                var graphqlRequest = new
                {
                    query = CollectionRevisionQuery,
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
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.OAuth.AccessToken);
                request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
                request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
                request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

                var response = await _httpClient.SendAsync(request, token);
                var responseBody = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GraphQL request failed: {status} - {body}",
                        response.StatusCode, responseBody);
                    return null;
                }

                // Parse response
                var root = JsonNode.Parse(responseBody) as JsonObject;
                var errors = root?["errors"] as JsonArray;
                if (errors is { Count: > 0 })
                {
                    _logger.LogError("GraphQL returned errors: {errors}", errors.ToJsonString());
                    return null;
                }

                var revisionData = root?["data"]?["collectionRevision"] as JsonObject;
                if (revisionData == null)
                {
                    _logger.LogError("No collection revision data in response");
                    return null;
                }

                var downloadLink = revisionData["downloadLink"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(downloadLink))
                {
                    _logger.LogError("No download link found in collection revision");
                    return null;
                }

                var collectionData = revisionData["collection"] as JsonObject;
                var collectionName = collectionData?["name"]?.GetValue<string>() ?? collectionSlug;
                var actualRevisionNumber = revisionData["revisionNumber"]?.GetValue<int>() ?? 1;
                var collectionId = revisionData["collectionId"]?.GetValue<int>() ?? 0;
                var revisionId = revisionData["id"]?.GetValue<int>() ?? 0;

                // Get game information
                var gameData = collectionData?["game"] as JsonObject;
                var gameDomainName = gameData?["domainName"]?.GetValue<string>();
                var gameDisplayName = gameData?["name"]?.GetValue<string>();

                _logger.LogInformation("Found collection: {name} (revision {revision}) for game: {game}",
                    collectionName, actualRevisionNumber, gameDisplayName ?? gameDomainName ?? "Unknown");

                // Check if the game is installed
                Game? requiredGame = null;
                string? gameName = null;

                if (!string.IsNullOrWhiteSpace(gameDomainName))
                {
                    var gameMetadata = GameRegistry.GetByNexusName(gameDomainName);
                    if (gameMetadata != null)
                    {
                        requiredGame = gameMetadata.Game;
                        gameName = gameMetadata.HumanFriendlyGameName;

                        if (!_gameLocator.IsInstalled(requiredGame.Value))
                        {
                            _logger.LogWarning("Cannot download collection '{name}': Required game '{game}' is not installed",
                                collectionName, gameName);
                            LastError = $"Cannot install '{collectionName}': {gameName} is not installed on this PC. Please install {gameName} first.";
                            return null;
                        }

                        _logger.LogInformation("Game check passed: {game} is installed", gameName);
                    }
                    else
                    {
                        _logger.LogWarning("Could not map Nexus game '{nexusGame}' to Wabbajack game enum", gameDomainName);
                    }
                }

                var downloadLinkUrl = downloadLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? downloadLink
                    : $"https://api.nexusmods.com{downloadLink}";

                _logger.LogInformation("Fetching download URL from: {link}", downloadLinkUrl);

                // Make authenticated request to get the actual download URL
                using var dlRequest = new HttpRequestMessage(HttpMethod.Get, downloadLinkUrl);
                dlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.OAuth.AccessToken);
                dlRequest.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
                dlRequest.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
                dlRequest.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

                var dlResponse = await _httpClient.SendAsync(dlRequest, token);
                var dlResponseBody = await dlResponse.Content.ReadAsStringAsync(token);

                if (!dlResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get download URL: {status} - {body}",
                        dlResponse.StatusCode, dlResponseBody);
                    return null;
                }

                // Parse the response structure is { "download_links": [{ "URI": "..." }] }
                var dlRoot = JsonNode.Parse(dlResponseBody) as JsonObject;
                var downloadLinks = dlRoot?["download_links"] as JsonArray;

                if (downloadLinks == null || downloadLinks.Count == 0)
                {
                    _logger.LogError("No download_links array in response");
                    return null;
                }

                var firstLink = downloadLinks[0] as JsonObject;
                var actualDownloadUrl = firstLink?["URI"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(actualDownloadUrl))
                {
                    _logger.LogError("No URI field in download link");
                    return null;
                }

                _logger.LogInformation("Got actual download URL: {url}", actualDownloadUrl);

                return new CollectionDownloadInfo
                {
                    CollectionSlug = collectionSlug,
                    CollectionName = collectionName,
                    CollectionId = collectionId,
                    RevisionNumber = actualRevisionNumber,
                    RevisionId = revisionId,
                    DownloadUrl = actualDownloadUrl,
                    DownloadLink = downloadLink,
                    GameDomainName = gameDomainName,
                    GameDisplayName = gameDisplayName,
                    RequiredGame = requiredGame
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get collection download info for {slug}", collectionSlug);
                return null;
            }
        }
    }

    public class CollectionDownloadInfo
    {
        public string CollectionSlug { get; set; } = "";
        public string CollectionName { get; set; } = "";
        public int CollectionId { get; set; }
        public int RevisionNumber { get; set; }
        public int RevisionId { get; set; }
        public string DownloadUrl { get; set; } = "";
        public string DownloadLink { get; set; } = "";
        public string? GameDomainName { get; set; }
        public string? GameDisplayName { get; set; }
        public Game? RequiredGame { get; set; }
    }
}