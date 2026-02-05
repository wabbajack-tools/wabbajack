using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.API;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Common;

namespace Wabbajack.Server;

/// <summary>
/// Service that downloads .wabbajack files, extracts modlist metadata,
/// and caches results by URL hash for the pre-install checks flow.
/// </summary>
public class ModlistPreparer : IDisposable
{
    private readonly ILogger<ModlistPreparer> _logger;
    private readonly ConcurrentDictionary<string, PreparedModlist> _cache = new();
    private readonly ConcurrentDictionary<string, ModlistPrepareStatus> _activeOps = new();
    private readonly TemporaryFileManager _tempManager;
    private readonly DownloadDispatcher _dispatcher;
    private readonly Client _wjClient;
    private readonly DTOSerializer _serializer;
    private readonly EventBroadcaster _eventBroadcaster;
    private readonly GameLocator _gameLocator;

    public ModlistPreparer(
        ILogger<ModlistPreparer> logger,
        TemporaryFileManager tempManager,
        DownloadDispatcher dispatcher,
        Client wjClient,
        DTOSerializer serializer,
        EventBroadcaster eventBroadcaster,
        GameLocator gameLocator)
    {
        _logger = logger;
        _tempManager = tempManager;
        _dispatcher = dispatcher;
        _wjClient = wjClient;
        _serializer = serializer;
        _eventBroadcaster = eventBroadcaster;
        _gameLocator = gameLocator;
    }

    /// <summary>
    /// Computes a session ID from the download URL using SHA256 hash (first 16 hex chars).
    /// </summary>
    public string ComputeSessionId(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Gets a prepared modlist by session ID if it exists in the cache.
    /// </summary>
    public PreparedModlist? GetPrepared(string sessionId)
    {
        return _cache.TryGetValue(sessionId, out var prepared) ? prepared : null;
    }

    /// <summary>
    /// Gets the current status of a preparation operation.
    /// </summary>
    public ModlistPrepareStatus? GetStatus(string sessionId)
    {
        return _activeOps.TryGetValue(sessionId, out var status) ? status : null;
    }

    /// <summary>
    /// Prepares a modlist by downloading and extracting its metadata.
    /// Returns immediately if already cached.
    /// </summary>
    public async Task<ModlistPrepareStatus> PrepareAsync(ModlistPrepareRequest request, CancellationToken token)
    {
        var sessionId = ComputeSessionId(request.DownloadUrl);

        // Check cache first
        if (_cache.ContainsKey(sessionId))
        {
            _logger.LogInformation("Modlist {SessionId} found in cache", sessionId);
            return new ModlistPrepareStatus(sessionId, "ready", 1.0, null);
        }

        // Check if already in progress
        if (_activeOps.TryGetValue(sessionId, out var existingStatus) &&
            existingStatus.Status is "downloading" or "extracting")
        {
            return existingStatus;
        }

        // Start the preparation
        var initialStatus = new ModlistPrepareStatus(sessionId, "downloading", 0.0, null);
        _activeOps[sessionId] = initialStatus;

        // Run in background
        _ = Task.Run(async () =>
        {
            try
            {
                await PrepareInternalAsync(sessionId, request, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare modlist {SessionId}", sessionId);
                _activeOps[sessionId] = new ModlistPrepareStatus(sessionId, "error", 0.0, ex.Message);
                _eventBroadcaster.BroadcastError($"Failed to prepare modlist: {ex.Message}");
            }
        }, token);

        return initialStatus;
    }

    private async Task PrepareInternalAsync(string sessionId, ModlistPrepareRequest request, CancellationToken token)
    {
        _logger.LogInformation("Starting download of modlist from {Url}", request.DownloadUrl);

        // Find the modlist metadata from the server
        var lists = await _wjClient.LoadLists();
        var listMeta = lists.FirstOrDefault(l => l.Links.Download == request.DownloadUrl);

        if (listMeta == null)
        {
            // Try matching by machineUrl if download URL didn't match
            listMeta = lists.FirstOrDefault(l => l.NamespacedName == request.MachineUrl);
        }

        if (listMeta?.DownloadMetadata == null)
        {
            throw new InvalidOperationException($"Could not find modlist metadata for {request.DownloadUrl}");
        }

        // Parse the download URL into a state
        var state = _dispatcher.Parse(new Uri(request.DownloadUrl));
        if (state == null)
        {
            throw new InvalidOperationException($"Cannot parse download URL: {request.DownloadUrl}");
        }

        // Create the archive descriptor
        var archive = new Archive
        {
            Name = $"{listMeta.Title}.wabbajack",
            Hash = listMeta.DownloadMetadata.Hash,
            Size = listMeta.DownloadMetadata.Size,
            State = state
        };

        // Create temp file for the download
        var tempPath = _tempManager.CreateFile(new Extension(".wabbajack"), deleteOnDispose: false);

        try
        {
            // Download using the dispatcher
            _logger.LogInformation("Downloading {Name} ({Size})", archive.Name, archive.Size.ToFileSizeString());
            _activeOps[sessionId] = new ModlistPrepareStatus(sessionId, "downloading", 0.1, null);

            var hash = await _dispatcher.Download(archive, tempPath.Path, token);

            if (hash != listMeta.DownloadMetadata.Hash)
            {
                throw new InvalidDataException($"Downloaded file hash mismatch. Expected {listMeta.DownloadMetadata.Hash}, got {hash}");
            }

            // Update status to extracting
            _activeOps[sessionId] = new ModlistPrepareStatus(sessionId, "extracting", 0.95, null);

            // Extract and parse the modlist
            _logger.LogInformation("Extracting modlist from {Path}", tempPath.Path);
            var modList = await LoadModlistFromFile(tempPath.Path);

            // Build the pre-install info
            var info = BuildPreInstallInfo(sessionId, modList);

            // Store in cache
            _cache[sessionId] = new PreparedModlist(
                SessionId: sessionId,
                ModList: modList,
                Info: info,
                TempFilePath: tempPath.Path,
                PreparedAt: DateTime.UtcNow);

            // Update status to ready
            _activeOps[sessionId] = new ModlistPrepareStatus(sessionId, "ready", 1.0, null);
            _logger.LogInformation("Modlist {SessionId} prepared successfully", sessionId);
        }
        catch (Exception)
        {
            // Clean up temp file on failure
            try { tempPath.Path.Delete(); } catch { }
            throw;
        }
    }

    private async Task<ModList> LoadModlistFromFile(AbsolutePath path)
    {
        await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var ar = new ZipArchive(fs, ZipArchiveMode.Read);

        var entry = ar.GetEntry("modlist") ?? ar.GetEntry("modlist.json");
        if (entry == null)
            throw new InvalidDataException("Invalid Wabbajack file: missing modlist entry");

        await using var entryStream = entry.Open();
        var modList = await _serializer.DeserializeAsync<ModList>(entryStream);

        if (modList == null)
            throw new InvalidDataException("Failed to deserialize modlist");

        return modList;
    }

    private ModlistPreInstallInfo BuildPreInstallInfo(string sessionId, ModList modList)
    {
        // Get game info
        var gameInstalled = _gameLocator.IsInstalled(modList.GameType);
        string? gamePath = null;
        if (gameInstalled)
        {
            gamePath = _gameLocator.GameLocation(modList.GameType).ToString();
        }

        // Count manual downloads (explicitly manual)
        var manualDownloadCount = modList.Archives
            .Count(a => a.State is Manual);

        // Count non-automatic downloads (everything except Nexus, WabbajackCDN, and Http/DirectLink)
        var nonAutomaticDownloadCount = modList.Archives
            .Count(a => a.State is not (Nexus or WabbajackCDN or Http));

        // Calculate sizes
        var totalArchiveSize = modList.Archives.Sum(a => a.Size);
        var totalInstalledSize = modList.Directives.Sum(d => d.Size);
        var estimatedTempSpace = totalArchiveSize + (totalInstalledSize / 2); // Rough estimate

        // Get game display name
        var gameDisplayName = GameRegistry.Games.TryGetValue(modList.GameType, out var gameInfo)
            ? gameInfo.HumanFriendlyGameName
            : modList.GameType.ToString();

        var basicInfo = new ModlistBasicInfo(
            Name: modList.Name,
            Author: modList.Author,
            Description: modList.Description,
            Version: modList.Version?.ToString() ?? "Unknown",
            GameType: modList.GameType.ToString(),
            GameDisplayName: gameDisplayName,
            IsNsfw: modList.IsNSFW,
            Website: string.IsNullOrEmpty(modList.Website) ? null : modList.Website,
            Readme: string.IsNullOrEmpty(modList.Readme) ? null : modList.Readme);

        var requirements = new InstallationRequirements(
            ArchiveCount: modList.Archives.Length,
            TotalArchiveSize: totalArchiveSize,
            DirectiveCount: modList.Directives.Length,
            TotalInstalledSize: totalInstalledSize,
            EstimatedTempSpace: estimatedTempSpace,
            GameInstalled: gameInstalled,
            GamePath: gamePath,
            ManualDownloadCount: manualDownloadCount,
            NonAutomaticDownloadCount: nonAutomaticDownloadCount);

        // Build warnings
        var warnings = new List<PreInstallWarning>();

        if (!gameInstalled)
        {
            warnings.Add(new PreInstallWarning(
                "game_not_installed",
                $"{gameDisplayName} is not installed. You need to install the game before installing this modlist."));
        }

        if (modList.IsNSFW)
        {
            warnings.Add(new PreInstallWarning(
                "nsfw",
                "This modlist contains adult content (NSFW)."));
        }

        if (nonAutomaticDownloadCount > 0)
        {
            warnings.Add(new PreInstallWarning(
                "non_automatic_downloads",
                $"This modlist requires {nonAutomaticDownloadCount} non-automatic download(s). These may require manual intervention or additional authentication."));
        }

        return new ModlistPreInstallInfo(
            SessionId: sessionId,
            Modlist: basicInfo,
            Requirements: requirements,
            Warnings: warnings);
    }

    public void Dispose()
    {
        // Clean up cached temp files
        foreach (var prepared in _cache.Values)
        {
            try
            {
                prepared.TempFilePath.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file {Path}", prepared.TempFilePath);
            }
        }
        _cache.Clear();
        _activeOps.Clear();
    }
}

/// <summary>
/// A prepared modlist stored in the cache.
/// </summary>
public record PreparedModlist(
    string SessionId,
    ModList ModList,
    ModlistPreInstallInfo Info,
    AbsolutePath TempFilePath,
    DateTime PreparedAt);
