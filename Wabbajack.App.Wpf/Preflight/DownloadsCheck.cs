using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Humanizer;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Preflight;

public partial class DownloadsCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();
    private readonly AbsolutePath _downloadDir;
    private readonly bool _isPremium;
    private readonly ILogger _logger;

    // All tracked archives with their sub-items
    private readonly List<(Archive Archive, PreflightSubItem Item, bool IsNexus)> _tracked = new();
    private readonly HashSet<Hash> _matchedHashes = new();
    private readonly object _lock = new();

    public string Title => "Downloads";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    [Reactive] public partial ICommand? ActionCommand { get; set; }
    [Reactive] public partial string? ActionLabel { get; set; }
    [Reactive] public partial IReadOnlyList<PreflightSubItem>? SubItems { get; set; }
    [Reactive] public partial int ReadyCount { get; set; }
    [Reactive] public partial int TotalTracked { get; set; }

    /// <summary>Total bytes of archives confirmed present in the download folder.</summary>
    public long PresentArchiveSize { get; private set; }

    public DownloadsCheck(IReadOnlyList<Archive> allArchives, AbsolutePath downloadDir,
        AbsolutePath systemDownloadsDir, bool isPremium, ILogger logger)
    {
        _downloadDir = downloadDir;
        _isPremium = isPremium;
        _logger = logger;

        // Build stable sub-item list for all non-auto archives
        foreach (var a in allArchives)
        {
            var isNexus = a.State is Nexus;
            var isManual = IsManualDownload(a);

            // Skip HTTP and GameFileSource — those are fully auto
            if (!isNexus && !isManual) continue;

            var url = GetDownloadUrl(a);
            var item = new PreflightSubItem
            {
                Name = a.Name,
                SizeText = a.Size.Bytes().ToString(),
                ActionCommand = string.IsNullOrEmpty(url) ? null : ReactiveCommand.Create(() => OpenUrl(url)),
                ActionLabel = string.IsNullOrEmpty(url) ? null : "Download"
            };

            _tracked.Add((a, item, isNexus));
        }

        _logger.LogInformation("Preflight downloads: tracking {Count} archives ({Nexus} Nexus, {Manual} manual)",
            _tracked.Count, _tracked.Count(t => t.IsNexus), _tracked.Count(t => !t.IsNexus));

        SubItems = _tracked.Select(t => t.Item).ToList();
        UpdateStatus();
        StartWatching(downloadDir, systemDownloadsDir);
    }

    private static bool IsManualDownload(Archive archive)
    {
        return archive.State switch
        {
            Http => false,
            GameFileSource => false,
            Nexus => false,
            _ => true,
        };
    }

    public async Task ScanExistingFiles(CancellationToken token)
    {
        if (!_downloadDir.DirectoryExists())
        {
            _logger.LogInformation("Preflight: download dir {Dir} does not exist, skipping scan", _downloadDir);
            return;
        }

        var files = _downloadDir.EnumerateFiles().ToList();
        _logger.LogInformation("Preflight: scanning {Count} existing files in {Dir}", files.Count, _downloadDir);

        foreach (var file in files)
        {
            await TryMatchFile(file, token);
        }
    }

    private async Task TryMatchFile(AbsolutePath filePath, CancellationToken token)
    {
        if (!filePath.FileExists()) return;

        // Wait for the file to be fully written — retry up to 5 times with backoff
        long fileSize = 0;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                fileSize = filePath.Size();
                // Try opening the file to check it's not locked
                await using var testStream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                break;
            }
            catch (IOException) when (attempt < 4)
            {
                _logger.LogDebug("Preflight: file {File} locked (attempt {Attempt}), retrying...",
                    filePath.FileName, attempt + 1);
                await Task.Delay(1000 * (attempt + 1), token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Preflight: cannot access file {File}", filePath.FileName);
                return;
            }
        }

        List<(Archive Archive, PreflightSubItem Item, bool IsNexus)> candidates;
        lock (_lock)
        {
            candidates = _tracked
                .Where(t => !t.Item.IsReady && t.Archive.Size == fileSize)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            _logger.LogDebug("Preflight: no size match for {File} ({Size} bytes)",
                filePath.FileName, fileSize);
            return;
        }

        _logger.LogInformation("Preflight: size match for {File} ({Size} bytes) — {Count} candidates: {Names}",
            filePath.FileName, fileSize, candidates.Count,
            string.Join(", ", candidates.Select(c => c.Archive.Name)));

        // Show verification progress on the first candidate's sub-item
        var subItem = candidates[0].Item;
        var fileName = filePath.FileName.ToString();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            subItem.StatusText = $"Verifying {fileName}...";
            subItem.Progress = 0;
        });

        Hash hash;
        try
        {
            await using var stream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var hasher = new XxHash64();
            var buffer = new byte[1024 * 1024];
            long totalRead = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0) break;
                hasher.Append(buffer.AsSpan(0, read));
                totalRead += read;
                if (fileSize > 0)
                {
                    var pct = (double)totalRead / fileSize;
                    RxApp.MainThreadScheduler.Schedule(() => subItem.Progress = pct);
                }
            }
            hash = new Hash(hasher.GetCurrentHashAsUInt64());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preflight: failed to hash {File}", filePath.FileName);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                subItem.StatusText = null;
                subItem.Progress = null;
            });
            return;
        }

        RxApp.MainThreadScheduler.Schedule(() =>
        {
            subItem.StatusText = null;
            subItem.Progress = null;
        });

        _logger.LogInformation("Preflight: hashed {File} = {Hash}", filePath.FileName, hash.ToHex());

        // Find which tracked archive matches this hash
        lock (_lock)
        {
            var match = _tracked.FirstOrDefault(t => !t.Item.IsReady && t.Archive.Hash == hash);
            if (match.Archive == null)
            {
                _logger.LogInformation("Preflight: hash {Hash} does not match any tracked archive", hash.ToHex());
                return;
            }

            _logger.LogInformation("Preflight: matched {File} to archive {Archive}", filePath.FileName, match.Archive.Name);

            // Move file to download dir if not already there
            var destPath = _downloadDir.Combine(match.Archive.Name);
            if (filePath != destPath)
            {
                try
                {
                    if (!_downloadDir.DirectoryExists())
                        _downloadDir.CreateDirectory();

                    if (destPath.FileExists()) destPath.Delete();
                    File.Move(filePath.ToString(), destPath.ToString());
                    _logger.LogInformation("Preflight: moved {Src} -> {Dest}", filePath, destPath);
                }
                catch (Exception moveEx)
                {
                    _logger.LogWarning(moveEx, "Preflight: move failed, trying copy");
                    try
                    {
                        File.Copy(filePath.ToString(), destPath.ToString(), true);
                        _logger.LogInformation("Preflight: copied {Src} -> {Dest}", filePath, destPath);
                    }
                    catch (Exception copyEx)
                    {
                        _logger.LogError(copyEx, "Preflight: copy also failed for {File}", filePath.FileName);
                        return;
                    }
                }
            }
            else
            {
                _logger.LogInformation("Preflight: {File} already in download dir", filePath.FileName);
            }

            _matchedHashes.Add(hash);
            PresentArchiveSize += match.Archive.Size;

            var matchedItem = match.Item;
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                matchedItem.IsReady = true;
                matchedItem.StatusText = null;
            });
        }

        RxApp.MainThreadScheduler.Schedule(UpdateStatus);
    }

    private void StartWatching(AbsolutePath downloadDir, AbsolutePath systemDownloadsDir)
    {
        WatchDirectory(downloadDir, "download");
        if (systemDownloadsDir != downloadDir && systemDownloadsDir != default)
            WatchDirectory(systemDownloadsDir, "system downloads");
    }

    private void WatchDirectory(AbsolutePath dir, string label)
    {
        if (dir == default) return;

        if (!dir.DirectoryExists())
            dir.CreateDirectory();

        _logger.LogInformation("Preflight: watching {Label} directory: {Dir}", label, dir);

        var watcher = new FileSystemWatcher(dir.ToString())
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        watcher.Created += (_, e) =>
        {
            _logger.LogDebug("Preflight: watcher [{Label}] Created: {File}", label, e.FullPath);
            OnFileChanged(e.FullPath);
        };
        watcher.Changed += (_, e) =>
        {
            _logger.LogDebug("Preflight: watcher [{Label}] Changed: {File}", label, e.FullPath);
            OnFileChanged(e.FullPath);
        };
        watcher.Renamed += (_, e) =>
        {
            _logger.LogDebug("Preflight: watcher [{Label}] Renamed: {Old} -> {New}", label, e.OldFullPath, e.FullPath);
            OnFileChanged(e.FullPath);
        };
        watcher.Error += (_, e) =>
        {
            _logger.LogError(e.GetException(), "Preflight: watcher [{Label}] error", label);
        };

        _disposable.Add(watcher);
    }

    private void OnFileChanged(string fullPath)
    {
        Task.Run(async () =>
        {
            // Wait for file to settle (browser may still be writing)
            await Task.Delay(2000);
            await TryMatchFile((AbsolutePath)fullPath, CancellationToken.None);
        });
    }

    private void UpdateStatus()
    {
        lock (_lock)
        {
            var readyCount = _tracked.Count(t => t.Item.IsReady);
            var missingManual = _tracked.Where(t => !t.IsNexus && !t.Item.IsReady).ToList();
            var missingNexus = _tracked.Where(t => t.IsNexus && !t.Item.IsReady).ToList();
            var allReady = readyCount == _tracked.Count;

            _logger.LogDebug("Preflight downloads status: {Ready}/{Total} ready, {MissingManual} manual missing, {MissingNexus} nexus missing",
                readyCount, _tracked.Count, missingManual.Count, missingNexus.Count);

            ReadyCount = readyCount;
            TotalTracked = _tracked.Count;

            // SubItems only shows non-ready items — ready ones are rolled up into ReadyCount
            SubItems = _tracked.Where(t => !t.Item.IsReady).Select(t => t.Item).ToList();

            var readySuffix = readyCount > 0 ? $" ({readyCount} of {_tracked.Count} ready)" : "";

            if (allReady)
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = $"All {_tracked.Count} files ready";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingManual.Count == 0 && missingNexus.Count > 0 && _isPremium)
            {
                Status = PreflightCheckStatus.Info;
                FailureMessage = $"{missingNexus.Count} Nexus files will be downloaded automatically during install{readySuffix}";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingNexus.Count > 0 && !_isPremium)
            {
                Status = PreflightCheckStatus.Failed;
                var total = missingNexus.Count + missingManual.Count;
                FailureMessage = missingManual.Count > 0
                    ? $"{total} files need downloading{readySuffix}"
                    : $"{missingNexus.Count} Nexus files require premium or manual download{readySuffix}";
                ActionCommand = ReactiveCommand.Create(() =>
                    OpenUrl("https://next.nexusmods.com/premium"));
                ActionLabel = "Get Nexus Premium";
            }
            else
            {
                Status = PreflightCheckStatus.Failed;
                FailureMessage = $"Download these files — they'll be detected automatically{readySuffix}";
                ActionCommand = null;
                ActionLabel = null;
            }
        }
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failures to open browser
        }
    }

    private static string GetDownloadUrl(Archive archive)
    {
        return archive.State switch
        {
            Manual manual => manual.Url?.ToString() ?? string.Empty,
            MediaFire mediaFire => mediaFire.Url?.ToString() ?? string.Empty,
            Mega mega => mega.Url?.ToString() ?? string.Empty,
            IPS4OAuth2 ips4 => ips4.LinkUrl?.ToString() ?? string.Empty,
            GoogleDrive gd => gd.GetUri()?.ToString() ?? string.Empty,
            Http http => http.Url?.ToString() ?? string.Empty,
            Nexus nexus => $"{nexus.LinkUrl}?tab=files&file_id={nexus.FileID}",
            _ => string.Empty
        };
    }

    public void Dispose() => _disposable.Dispose();
}
