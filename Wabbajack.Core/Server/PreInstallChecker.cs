using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.API;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server;

/// <summary>
/// Main service for managing pre-install checklist state and performing checks.
/// </summary>
public class PreInstallChecker
{
    private readonly ILogger<PreInstallChecker> _logger;
    private readonly GameLocator _gameLocator;
    private readonly FileScanner _fileScanner;
    private readonly PathValidator _pathValidator;
    private readonly ModlistPreparer _modlistPreparer;
    private readonly EventBroadcaster _eventBroadcaster;
    private readonly ITokenProvider<NexusOAuthState>? _nexusAuth;

    private readonly ConcurrentDictionary<string, PreInstallChecklistState> _states = new();

    public PreInstallChecker(
        ILogger<PreInstallChecker> logger,
        GameLocator gameLocator,
        FileScanner fileScanner,
        PathValidator pathValidator,
        ModlistPreparer modlistPreparer,
        EventBroadcaster eventBroadcaster,
        ITokenProvider<NexusOAuthState>? nexusAuth = null)
    {
        _logger = logger;
        _gameLocator = gameLocator;
        _fileScanner = fileScanner;
        _pathValidator = pathValidator;
        _modlistPreparer = modlistPreparer;
        _eventBroadcaster = eventBroadcaster;
        _nexusAuth = nexusAuth;
    }

    /// <summary>
    /// Validates install and download folder paths.
    /// </summary>
    public PathValidationResult ValidatePaths(string sessionId, string installFolder, string downloadFolder)
    {
        var prepared = _modlistPreparer.GetPrepared(sessionId);
        if (prepared == null)
        {
            _logger.LogWarning("Session {SessionId} not found for path validation", sessionId);
            return new PathValidationResult(
                new FolderValidation(false, new List<string> { "Session not found" }, new List<string>()),
                new FolderValidation(false, new List<string> { "Session not found" }, new List<string>()));
        }

        var game = prepared.ModList.GameType;
        var result = _pathValidator.ValidatePaths(installFolder, downloadFolder, game);

        // Update state
        UpdateState(sessionId, state => state with { PathValidation = result });

        return result;
    }

    /// <summary>
    /// Checks if the user is logged into Nexus Mods.
    /// </summary>
    public NexusLoginStatus CheckNexusLogin()
    {
        if (_nexusAuth == null)
        {
            _logger.LogDebug("Nexus auth provider not available");
            return new NexusLoginStatus(false, null);
        }

        var hasToken = _nexusAuth.HaveToken();
        // Note: To get the username, we'd need to call the Nexus API
        // For now, just return whether a token exists
        return new NexusLoginStatus(hasToken, hasToken ? "Nexus User" : null);
    }

    /// <summary>
    /// Checks game files required by the modlist.
    /// </summary>
    public async Task<GameFilesCheckResult> CheckGameFilesAsync(string sessionId, CancellationToken token)
    {
        var prepared = _modlistPreparer.GetPrepared(sessionId);
        if (prepared == null)
        {
            _logger.LogWarning("Session {SessionId} not found for game files check", sessionId);
            return new GameFilesCheckResult("error", 0, 0, new List<GameFileStatus>());
        }

        var game = prepared.ModList.GameType;
        if (!_gameLocator.TryFindLocation(game, out var gamePath))
        {
            _logger.LogWarning("Game {Game} not installed", game);
            return new GameFilesCheckResult("game_not_installed", prepared.GameFileArchives.Count, 0, new List<GameFileStatus>());
        }

        // Build expected files list
        var expectedFiles = prepared.GameFileArchives
            .Select(a => new ExpectedFile(
                Path.GetFileName(a.GameFile),
                a.GameFile,
                a.Size,
                a.Hash))
            .ToList();

        if (expectedFiles.Count == 0)
        {
            return new GameFilesCheckResult("complete", 0, 0, new List<GameFileStatus>());
        }

        // Scan for files
        var results = await _fileScanner.ScanFilesAsync(gamePath, expectedFiles, sessionId, token);

        // Convert to API format
        var fileStatuses = results.Select(r => new GameFileStatus(
            r.RelativePath,
            r.Status switch
            {
                FileMatchStatus.Found => "found",
                FileMatchStatus.NotFound => "missing",
                FileMatchStatus.SizeMismatch => "size_mismatch",
                FileMatchStatus.HashMismatch => "hash_mismatch",
                _ => "unknown"
            },
            expectedFiles.First(e => e.FileName == r.FileName).ExpectedHash.ToString(),
            r.ActualHash?.ToString())).ToList();

        var foundCount = results.Count(r => r.Status == FileMatchStatus.Found);
        var status = foundCount == results.Count ? "complete" : "incomplete";

        var result = new GameFilesCheckResult(status, results.Count, foundCount, fileStatuses);

        // Update state
        UpdateState(sessionId, state => state with { GameFilesCheck = result });

        return result;
    }

    /// <summary>
    /// Checks manual downloads required by the modlist.
    /// </summary>
    public async Task<ManualDownloadsCheckResult> CheckManualDownloadsAsync(
        string sessionId,
        string downloadFolder,
        CancellationToken token)
    {
        var prepared = _modlistPreparer.GetPrepared(sessionId);
        if (prepared == null)
        {
            _logger.LogWarning("Session {SessionId} not found for manual downloads check", sessionId);
            return new ManualDownloadsCheckResult("error", 0, 0, new List<ManualDownloadStatus>());
        }

        if (prepared.ManualArchives.Count == 0)
        {
            return new ManualDownloadsCheckResult("complete", 0, 0, new List<ManualDownloadStatus>());
        }

        // Build expected files list
        var expectedFiles = prepared.ManualArchives
            .Select(a => new ExpectedFile(a.Name, a.Name, a.Size, a.Hash))
            .ToList();

        var downloadPath = string.IsNullOrEmpty(downloadFolder) ? default : downloadFolder.ToAbsolutePath();

        // Scan for manual downloads
        var matches = await _fileScanner.ScanForManualDownloadsAsync(
            expectedFiles, downloadPath, sessionId, token);

        // Convert to API format
        var fileStatuses = new List<ManualDownloadStatus>();
        for (int i = 0; i < prepared.ManualArchives.Count; i++)
        {
            var archive = prepared.ManualArchives[i];
            var match = matches[i];

            var status = match.Location switch
            {
                ManualDownloadLocation.DownloadFolder => "ready",
                ManualDownloadLocation.OsDownloads => "found_in_os_downloads",
                _ => "missing"
            };

            fileStatuses.Add(new ManualDownloadStatus(
                archive.Name,
                archive.Url,
                archive.Prompt,
                status,
                archive.Size,
                archive.Hash.ToString(),
                match.FoundPath != default ? match.FoundPath.ToString() : null,
                GetFaviconForUrl(archive.Url)));
        }

        var foundCount = fileStatuses.Count(f => f.Status == "ready");
        var overallStatus = foundCount == fileStatuses.Count ? "complete" : "incomplete";

        var result = new ManualDownloadsCheckResult(overallStatus, fileStatuses.Count, foundCount, fileStatuses);

        // Update state
        UpdateState(sessionId, state => state with { ManualDownloadsCheck = result });

        return result;
    }

    /// <summary>
    /// Moves a file from source to the downloads folder.
    /// </summary>
    public async Task<bool> MoveDownloadFileAsync(string sourcePath, string downloadFolder, CancellationToken token)
    {
        try
        {
            var source = sourcePath.ToAbsolutePath();
            var destFolder = downloadFolder.ToAbsolutePath();

            if (!source.FileExists())
            {
                _logger.LogWarning("Source file does not exist: {Source}", source);
                return false;
            }

            if (!destFolder.DirectoryExists())
            {
                destFolder.CreateDirectory();
            }

            var dest = destFolder.Combine(source.FileName);

            // Use copy + delete for cross-filesystem support
            await using (var sourceStream = source.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var destStream = dest.Open(FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await sourceStream.CopyToAsync(destStream, token);
            }

            source.Delete();
            _logger.LogInformation("Moved file from {Source} to {Dest}", source, dest);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file from {Source} to {Dest}", sourcePath, downloadFolder);
            return false;
        }
    }

    /// <summary>
    /// Checks disk space for download and install folders.
    /// </summary>
    public DiskSpaceCheckResult CheckDiskSpace(string sessionId, string installFolder, string downloadFolder)
    {
        var prepared = _modlistPreparer.GetPrepared(sessionId);
        if (prepared == null)
        {
            _logger.LogWarning("Session {SessionId} not found for disk space check", sessionId);
            return new DiskSpaceCheckResult(
                new DriveSpaceInfo("", 0, 0, false),
                new DriveSpaceInfo("", 0, 0, false),
                false);
        }

        var requirements = prepared.Info.Requirements;
        var downloadRequired = requirements.TotalArchiveSize;
        var installRequired = requirements.TotalInstalledSize + requirements.EstimatedTempSpace;

        var downloadDrive = GetDriveSpace(downloadFolder, downloadRequired);
        var installDrive = GetDriveSpace(installFolder, installRequired);

        var areSameDrive = GetDriveRoot(downloadFolder) == GetDriveRoot(installFolder);

        var result = new DiskSpaceCheckResult(downloadDrive, installDrive, areSameDrive);

        // Update state
        UpdateState(sessionId, state => state with { DiskSpaceCheck = result });

        return result;
    }

    /// <summary>
    /// Gets the complete checklist state for a session.
    /// </summary>
    public PreInstallChecklistState? GetChecklistState(string sessionId)
    {
        if (_states.TryGetValue(sessionId, out var state))
        {
            return UpdateCanProceed(state);
        }

        // Initialize state if session exists but state doesn't
        var prepared = _modlistPreparer.GetPrepared(sessionId);
        if (prepared != null)
        {
            var newState = new PreInstallChecklistState(
                sessionId, null, null, null, null, null, false, new List<string>());
            _states[sessionId] = newState;
            return newState;
        }

        return null;
    }

    private void UpdateState(string sessionId, Func<PreInstallChecklistState, PreInstallChecklistState> updater)
    {
        _states.AddOrUpdate(
            sessionId,
            _ => updater(new PreInstallChecklistState(sessionId, null, null, null, null, null, false, new List<string>())),
            (_, existing) => updater(existing));
    }

    private PreInstallChecklistState UpdateCanProceed(PreInstallChecklistState state)
    {
        var issues = new List<string>();

        // Check path validation
        if (state.PathValidation == null)
        {
            issues.Add("Install and download folders not validated");
        }
        else
        {
            if (!state.PathValidation.InstallFolder.IsValid)
                issues.Add("Install folder has errors");
            if (!state.PathValidation.DownloadFolder.IsValid)
                issues.Add("Download folder has errors");
        }

        // Check Nexus login
        if (state.NexusLogin == null || !state.NexusLogin.IsLoggedIn)
        {
            issues.Add("Not logged into Nexus Mods");
        }

        // Check game files
        if (state.GameFilesCheck != null && state.GameFilesCheck.Status != "complete")
        {
            var missing = state.GameFilesCheck.Files.Count(f => f.Status != "found");
            if (missing > 0)
                issues.Add($"{missing} game file(s) missing or mismatched");
        }

        // Check manual downloads
        if (state.ManualDownloadsCheck != null && state.ManualDownloadsCheck.Status != "complete")
        {
            var missing = state.ManualDownloadsCheck.Files.Count(f => f.Status == "missing");
            if (missing > 0)
                issues.Add($"{missing} manual download(s) still needed");
        }

        // Check disk space
        if (state.DiskSpaceCheck != null)
        {
            if (!state.DiskSpaceCheck.DownloadDrive.HasEnoughSpace)
                issues.Add("Not enough space for downloads");
            if (!state.DiskSpaceCheck.InstallDrive.HasEnoughSpace)
                issues.Add("Not enough space for installation");
        }

        return state with
        {
            CanProceed = issues.Count == 0,
            BlockingIssues = issues
        };
    }

    private DriveSpaceInfo GetDriveSpace(string folder, long requiredSpace)
    {
        try
        {
            var path = folder.ToAbsolutePath();
            var root = GetDriveRoot(folder);

            var driveInfo = new DriveInfo(root);
            var available = driveInfo.AvailableFreeSpace;

            return new DriveSpaceInfo(
                root,
                available,
                requiredSpace,
                available >= requiredSpace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get drive info for {Folder}", folder);
            return new DriveSpaceInfo(folder, 0, requiredSpace, false);
        }
    }

    private string GetDriveRoot(string path)
    {
        try
        {
            return Path.GetPathRoot(path) ?? path;
        }
        catch
        {
            return path;
        }
    }

    private static string? GetFaviconForUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            // Known favicon mappings
            var favicons = new Dictionary<string, string>
            {
                { "nexusmods.com", "https://www.nexusmods.com/favicon.ico" },
                { "www.nexusmods.com", "https://www.nexusmods.com/favicon.ico" },
                { "loverslab.com", "https://www.loverslab.com/favicon.ico" },
                { "www.loverslab.com", "https://www.loverslab.com/favicon.ico" },
                { "patreon.com", "https://c5.patreon.com/external/favicon/favicon.ico" },
                { "www.patreon.com", "https://c5.patreon.com/external/favicon/favicon.ico" },
                { "drive.google.com", "https://ssl.gstatic.com/docs/doclist/images/drive_2022q3_32dp.png" },
                { "mega.nz", "https://mega.nz/favicon.ico" },
                { "mediafire.com", "https://www.mediafire.com/favicon.ico" },
                { "www.mediafire.com", "https://www.mediafire.com/favicon.ico" },
                { "moddb.com", "https://www.moddb.com/favicon.ico" },
                { "www.moddb.com", "https://www.moddb.com/favicon.ico" },
            };

            if (favicons.TryGetValue(host, out var favicon))
                return favicon;

            // Fallback to generic favicon URL
            return $"https://{host}/favicon.ico";
        }
        catch
        {
            return null;
        }
    }
}
