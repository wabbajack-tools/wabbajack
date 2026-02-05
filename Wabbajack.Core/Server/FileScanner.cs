using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.Server;

/// <summary>
/// Scans folders to match files by name, size, and hash.
/// Uses a name -> size -> hash strategy for efficient matching.
/// </summary>
public class FileScanner
{
    private readonly ILogger<FileScanner> _logger;
    private readonly FileHashCache _fileHashCache;
    private readonly EventBroadcaster _eventBroadcaster;

    public FileScanner(
        ILogger<FileScanner> logger,
        FileHashCache fileHashCache,
        EventBroadcaster eventBroadcaster)
    {
        _logger = logger;
        _fileHashCache = fileHashCache;
        _eventBroadcaster = eventBroadcaster;
    }

    /// <summary>
    /// Scans a folder to find files matching the expected list.
    /// Uses parallel processing with progress reporting.
    /// </summary>
    public async Task<List<FileMatchResult>> ScanFilesAsync(
        AbsolutePath folder,
        List<ExpectedFile> expectedFiles,
        string sessionId,
        CancellationToken token)
    {
        if (!folder.DirectoryExists())
        {
            _logger.LogWarning("Folder does not exist: {Folder}", folder);
            return expectedFiles.Select(f => new FileMatchResult(
                f.FileName,
                f.RelativePath,
                FileMatchStatus.NotFound,
                null,
                null)).ToList();
        }

        var total = expectedFiles.Count;
        var completed = 0;

        _eventBroadcaster.BroadcastChecklistProgress(sessionId, "GameFiles", 0, $"Starting scan of {total} files...");

        // Build a lookup by filename for faster matching
        var expectedByName = expectedFiles
            .GroupBy(f => f.FileName.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all files in the folder with names that match expected files
        var allFiles = folder.EnumerateFiles()
            .Select(f => new ScannedFile(f, f.FileName.ToString().ToLowerInvariant()))
            .Where(f => expectedByName.ContainsKey(f.Name))
            .ToList();

        var results = new List<FileMatchResult>();

        foreach (var expected in expectedFiles)
        {
            token.ThrowIfCancellationRequested();

            var result = await MatchFileAsync(folder, expected, allFiles, token);
            results.Add(result);

            completed++;
            var progress = (double)completed / total;
            _eventBroadcaster.BroadcastChecklistProgress(
                sessionId, "GameFiles", progress,
                $"Checked {completed}/{total}: {expected.FileName}");
        }

        _eventBroadcaster.BroadcastChecklistProgress(sessionId, "GameFiles", 1.0, "Scan complete");

        return results;
    }

    /// <summary>
    /// Matches a single file using name -> size -> hash strategy.
    /// </summary>
    private async Task<FileMatchResult> MatchFileAsync(
        AbsolutePath folder,
        ExpectedFile expected,
        List<ScannedFile> allFiles,
        CancellationToken token)
    {
        var expectedName = expected.FileName.ToLowerInvariant();

        // Step 1: Find files by name
        var nameMatches = allFiles
            .Where(f => f.Name == expectedName)
            .Select(f => f.Path)
            .ToList();

        if (nameMatches.Count == 0)
        {
            _logger.LogDebug("File not found by name: {FileName}", expected.FileName);
            return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.NotFound, null, null);
        }

        // Step 2: Filter by size (fast, no I/O beyond stat)
        var sizeMatches = nameMatches
            .Where(f => f.Size() == expected.ExpectedSize)
            .ToList();

        if (sizeMatches.Count == 0)
        {
            _logger.LogDebug("File found but size mismatch: {FileName}", expected.FileName);
            return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.SizeMismatch, nameMatches.First(), null);
        }

        // Step 3: Verify hash
        if (sizeMatches.Count == 1)
        {
            // Single match - verify hash
            var file = sizeMatches[0];
            var hash = await _fileHashCache.FileHashCachedAsync(file, token);

            if (hash == expected.ExpectedHash)
            {
                return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.Found, file, hash);
            }
            else
            {
                _logger.LogDebug("File found but hash mismatch: {FileName}", expected.FileName);
                return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.HashMismatch, file, hash);
            }
        }

        // Multiple size matches - hash each until we find a match
        foreach (var file in sizeMatches)
        {
            token.ThrowIfCancellationRequested();

            var hash = await _fileHashCache.FileHashCachedAsync(file, token);
            if (hash == expected.ExpectedHash)
            {
                return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.Found, file, hash);
            }
        }

        // No hash match found among size matches
        return new FileMatchResult(expected.FileName, expected.RelativePath, FileMatchStatus.HashMismatch, sizeMatches.First(), null);
    }

    /// <summary>
    /// Scans multiple folders for manual downloads.
    /// Searches in order: OS Downloads folder, then Wabbajack downloads folder.
    /// </summary>
    public async Task<List<ManualDownloadMatch>> ScanForManualDownloadsAsync(
        List<ExpectedFile> expectedFiles,
        AbsolutePath downloadFolder,
        string sessionId,
        CancellationToken token)
    {
        var osDownloadsFolder = PathValidator.GetOsDownloadsFolder();
        var results = new List<ManualDownloadMatch>();

        var total = expectedFiles.Count;
        var completed = 0;

        _eventBroadcaster.BroadcastChecklistProgress(sessionId, "ManualDownloads", 0, $"Scanning for {total} manual downloads...");

        foreach (var expected in expectedFiles)
        {
            token.ThrowIfCancellationRequested();

            ManualDownloadMatch? match = null;

            // First check the Wabbajack downloads folder
            if (downloadFolder != default && downloadFolder.DirectoryExists())
            {
                var found = await FindFileInFolderAsync(downloadFolder, expected, token);
                if (found != null)
                {
                    match = new ManualDownloadMatch(expected, found.Value, ManualDownloadLocation.DownloadFolder);
                }
            }

            // Then check OS Downloads folder
            if (match == null && osDownloadsFolder != default && osDownloadsFolder.DirectoryExists())
            {
                var found = await FindFileInFolderAsync(osDownloadsFolder, expected, token);
                if (found != null)
                {
                    match = new ManualDownloadMatch(expected, found.Value, ManualDownloadLocation.OsDownloads);
                }
            }

            results.Add(match ?? new ManualDownloadMatch(expected, default, ManualDownloadLocation.NotFound));

            completed++;
            var progress = (double)completed / total;
            _eventBroadcaster.BroadcastChecklistProgress(
                sessionId, "ManualDownloads", progress,
                $"Checked {completed}/{total}: {expected.FileName}");
        }

        _eventBroadcaster.BroadcastChecklistProgress(sessionId, "ManualDownloads", 1.0, "Scan complete");

        return results;
    }

    private async Task<AbsolutePath?> FindFileInFolderAsync(AbsolutePath folder, ExpectedFile expected, CancellationToken token)
    {
        var expectedName = expected.FileName.ToLowerInvariant();

        // Find files by name
        var candidates = folder.EnumerateFiles()
            .Where(f => f.FileName.ToString().ToLowerInvariant() == expectedName)
            .ToList();

        foreach (var candidate in candidates)
        {
            // Check size first (fast)
            if (candidate.Size() != expected.ExpectedSize)
                continue;

            // Check hash
            var hash = await _fileHashCache.FileHashCachedAsync(candidate, token);
            if (hash == expected.ExpectedHash)
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>
/// A file found during folder scanning with its normalized name.
/// </summary>
public record ScannedFile(AbsolutePath Path, string Name);

/// <summary>
/// Expected file to find during scanning.
/// </summary>
public record ExpectedFile(
    string FileName,
    string RelativePath,
    long ExpectedSize,
    Hash ExpectedHash);

/// <summary>
/// Result of a file match operation.
/// </summary>
public record FileMatchResult(
    string FileName,
    string RelativePath,
    FileMatchStatus Status,
    AbsolutePath? FoundPath,
    Hash? ActualHash);

/// <summary>
/// Status of a file match operation.
/// </summary>
public enum FileMatchStatus
{
    NotFound,
    SizeMismatch,
    HashMismatch,
    Found
}

/// <summary>
/// Match result for a manual download.
/// </summary>
public record ManualDownloadMatch(
    ExpectedFile Expected,
    AbsolutePath FoundPath,
    ManualDownloadLocation Location);

/// <summary>
/// Where a manual download was found.
/// </summary>
public enum ManualDownloadLocation
{
    NotFound,
    OsDownloads,
    DownloadFolder
}
