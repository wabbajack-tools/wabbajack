using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Humanizer;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.Preflight;

public partial class DownloadsCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly AbsolutePath _downloadDir;
    private readonly ILogger _logger;
    private readonly DownloadDispatcher? _downloadDispatcher;
    private readonly FileHashCache? _hashCache;
    [Reactive] public partial bool IsPremium { get; set; }

    // All tracked archives with their sub-items (NeedsPremium = true for Nexus only)
    private readonly List<(Archive Archive, PreflightSubItem Item, bool NeedsPremium)> _tracked = new();
    private readonly HashSet<Hash> _matchedHashes = new();
    private readonly HashSet<string> _activeDownloads = new(); // filenames being auto-downloaded
    private readonly object _lock = new();
    private int _updateStatusPending; // 0 = not pending, 1 = pending (for Interlocked)
    private volatile bool _scanComplete; // true once StartScanExistingFiles finishes

    // Auto-download state
    private CancellationTokenSource? _autoDownloadCts;
    [Reactive] public partial bool IsAutoDownloading { get; set; }

    // Pre-created commands (avoid recreating on every UpdateStatus call)
    private readonly ICommand _startAutoDownloadCommand;
    private readonly ICommand _pauseAutoDownloadCommand;
    private readonly ICommand _getNexusPremiumCommand;

    private const int MaxConcurrentDownloads = 4;
    private const int MaxDownloadRetries = 3;
    private const int ProgressPollIntervalMs = 2000;
    private const int WatcherSettleDelayMs = 2000;
    private const int UpdateStatusDebounceMs = 1000;

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
        AbsolutePath systemDownloadsDir, bool isPremium, ILogger logger,
        DownloadDispatcher? downloadDispatcher = null,
        FileHashCache? hashCache = null)
    {
        _downloadDir = downloadDir;
        IsPremium = isPremium;
        _logger = logger;
        _downloadDispatcher = downloadDispatcher;
        _hashCache = hashCache;

        _startAutoDownloadCommand = ReactiveCommand.Create(StartAutoDownload);
        _pauseAutoDownloadCommand = ReactiveCommand.Create(PauseAutoDownload);
        _getNexusPremiumCommand = ReactiveCommand.Create(() => OpenUrl("https://next.nexusmods.com/premium"));

        // Re-evaluate status when premium state changes
        Observable.Skip(this.WhenAnyValue(x => x.IsPremium), 1)
            .Subscribe(_ =>
            {
                _logger.LogInformation("Preflight: premium status changed to {IsPremium}", IsPremium);
                UpdateStatus();
            })
            .DisposeWith(_disposable);

        // Build sub-item list for all downloadable archives
        foreach (var a in allArchives)
        {
            // GameFileSource files are copied from the game folder, not downloaded
            if (a.State is GameFileSource) continue;

            var needsPremium = a.State is Nexus;
            var isManual = IsManualDownload(a);
            var isAutoDownloadable = !isManual; // Nexus, Http, WabbajackCDN

            var url = GetDownloadUrl(a);
            var item = new PreflightSubItem
            {
                Name = a.Name,
                SizeBytes = a.Size,
                SizeText = a.Size.Bytes().ToString(),
                // Manual items get a browser link; auto-downloadable items get a button that starts auto-download
                ActionCommand = isAutoDownloadable
                    ? _startAutoDownloadCommand
                    : string.IsNullOrEmpty(url) ? null : ReactiveCommand.Create(() => OpenUrl(url)),
                ActionLabel = isAutoDownloadable ? "Download" : (string.IsNullOrEmpty(url) ? null : "Download")
            };

            _tracked.Add((a, item, needsPremium));
        }

        var nexusCount = _tracked.Count(t => t.NeedsPremium);
        var autoFreeCount = _tracked.Count(t => !t.NeedsPremium && !IsManualDownload(t.Archive));
        var manualCount = _tracked.Count(t => IsManualDownload(t.Archive));
        _logger.LogInformation("Preflight downloads: tracking {Count} archives ({Nexus} Nexus, {AutoFree} HTTP/CDN, {Manual} manual)",
            _tracked.Count, nexusCount, autoFreeCount, manualCount);

        SubItems = _tracked.Where(t => !t.Item.IsReady).OrderByDescending(t => t.Archive.Size).Select(t => t.Item).ToList();
        UpdateStatus();
        StartWatching(downloadDir, systemDownloadsDir);
    }

    private static bool IsManualDownload(Archive archive)
    {
        return archive.State switch
        {
            Http => false,
            WabbajackCDN => false,
            GameFileSource => false,
            Nexus => false,
            _ => true,
        };
    }

    /// <summary>
    /// Scan existing files in the download directory. Uses hash cache for speed.
    /// Runs on a background thread and updates UI reactively.
    /// </summary>
    public void StartScanExistingFiles()
    {
        if (!_downloadDir.DirectoryExists())
        {
            _logger.LogInformation("Preflight: download dir {Dir} does not exist, skipping scan", _downloadDir);
            _scanComplete = true;
            ScheduleUpdateStatus();
            return;
        }

        var files = _downloadDir.EnumerateFiles().ToList();
        _logger.LogInformation("Preflight: scanning {Count} existing files in {Dir}", files.Count, _downloadDir);

        // Build a hash→archive lookup for fast matching
        Dictionary<Hash, (Archive Archive, PreflightSubItem Item)> hashLookup;
        lock (_lock)
        {
            hashLookup = _tracked
                .Where(t => !t.Item.IsReady)
                .ToDictionary(t => t.Archive.Hash, t => (t.Archive, t.Item));
        }

        // Also build a size→archives lookup for files not in cache
        Dictionary<long, List<(Archive Archive, PreflightSubItem Item)>> sizeLookup;
        lock (_lock)
        {
            sizeLookup = _tracked
                .Where(t => !t.Item.IsReady)
                .GroupBy(t => t.Archive.Size)
                .ToDictionary(g => g.Key, g => g.Select(t => (t.Archive, t.Item)).ToList());
        }

        Task.Run(async () =>
        {
            foreach (var file in files)
            {
                try
                {
                    // Quick size filter — skip files that can't possibly match any tracked archive
                    long fileSize;
                    try { fileSize = file.Size(); }
                    catch { continue; }

                    if (!sizeLookup.ContainsKey(fileSize)) continue;

                    // Find the UI item to show progress on (first size-matched candidate)
                    var candidates = sizeLookup[fileSize];
                    var uiItem = candidates[0].Item;
                    var fileName = file.FileName.ToString();

                    Hash hash = default;
                    if (_hashCache != null)
                    {
                        // Check cache without hashing first
                        hash = await _hashCache.TryGetHashCache(file);
                        if (hash == default)
                        {
                            // Cache miss — need to hash the full file. Show UI progress.
                            RxApp.MainThreadScheduler.Schedule(() =>
                            {
                                uiItem.StatusText = $"Verifying {fileName}...";
                                uiItem.Progress = null;
                            });

                            hash = await _hashCache.FileHashCachedAsync(file, CancellationToken.None);

                            RxApp.MainThreadScheduler.Schedule(() =>
                            {
                                uiItem.StatusText = null;
                                uiItem.Progress = null;
                            });
                        }
                    }
                    else
                    {
                        await TryMatchFile(file, CancellationToken.None);
                        continue;
                    }

                    if (hash == default) continue;

                    if (hashLookup.TryGetValue(hash, out var match))
                    {
                        _logger.LogInformation("Preflight: cached hash match for {File}", file.FileName);
                        lock (_lock)
                        {
                            _matchedHashes.Add(hash);
                            PresentArchiveSize += match.Archive.Size;
                        }
                        hashLookup.Remove(hash);
                        MarkItemCompleted(match.Item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Preflight: error scanning {File}", file.FileName);
                }
            }

            _scanComplete = true;
            _logger.LogInformation("Preflight: scan complete");
            ScheduleUpdateStatus();
        });
    }

    /// <summary>
    /// Start auto-downloading all missing Nexus archives. Premium only.
    /// Uses a simple worker-pool pattern: N workers pull from a shared queue,
    /// so exactly MaxConcurrentDownloads are in-flight at any time.
    /// </summary>
    public void StartAutoDownload()
    {
        if (_downloadDispatcher == null || IsAutoDownloading) return;

        IsAutoDownloading = true;
        _autoDownloadCts = new CancellationTokenSource();
        var token = _autoDownloadCts.Token;

        if (!_downloadDir.DirectoryExists())
            _downloadDir.CreateDirectory();

        ConcurrentQueue<(Archive Archive, PreflightSubItem Item)> queue;
        lock (_lock)
        {
            // Queue all auto-downloadable items: Http/CDN always, Nexus only if premium
            queue = new ConcurrentQueue<(Archive, PreflightSubItem)>(
                _tracked.Where(t => !t.Item.IsReady && !IsManualDownload(t.Archive)
                                    && (!t.NeedsPremium || IsPremium))
                    .Select(t => (t.Archive, t.Item)));
        }

        if (queue.IsEmpty)
        {
            IsAutoDownloading = false;
            return;
        }

        _logger.LogInformation("Preflight: starting auto-download of {Count} archives ({Max} workers)",
            queue.Count, MaxConcurrentDownloads);

        Task.Run(async () =>
        {
            var workers = Enumerable.Range(0, MaxConcurrentDownloads)
                .Select(_ => DownloadWorkerAsync(queue, token));
            await Task.WhenAll(workers);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                IsAutoDownloading = false;
                UpdateStatus();
            });
        }, token);
    }

    private async Task DownloadWorkerAsync(
        ConcurrentQueue<(Archive Archive, PreflightSubItem Item)> queue,
        CancellationToken token)
    {
        while (queue.TryDequeue(out var entry))
        {
            if (token.IsCancellationRequested) return;
            await DownloadOneAsync(entry.Archive, entry.Item, token);
        }
    }

    private async Task DownloadOneAsync(Archive archive, PreflightSubItem item, CancellationToken token)
    {
        var destPath = _downloadDir.Combine(archive.Name);
        var fileName = destPath.FileName.ToString();

        lock (_lock) { _activeDownloads.Add(fileName); }

        try
        {
            for (var attempt = 1; attempt <= MaxDownloadRetries; attempt++)
            {
                token.ThrowIfCancellationRequested();

                var prefix = attempt > 1 ? $"Retry {attempt}/{MaxDownloadRetries}: " : "";
                SetItemStatus(item, $"{prefix}Starting download...", null);
                _logger.LogInformation("Preflight: downloading {Name} (attempt {Attempt})", archive.Name, attempt);

                // Download with progress polling
                var hash = await DownloadWithProgress(archive, destPath, item, prefix, token);

                if (hash == archive.Hash)
                {
                    _logger.LogInformation("Preflight: download complete: {Name}", archive.Name);
                    lock (_lock)
                    {
                        _matchedHashes.Add(hash);
                        PresentArchiveSize += archive.Size;
                    }
                    MarkItemCompleted(item);
                    return;
                }

                // Hash mismatch — delete bad file, purge cache, retry
                _logger.LogWarning(
                    "Preflight: hash mismatch for {Name} (attempt {Attempt}): expected {Expected}, got {Actual}",
                    archive.Name, attempt, archive.Hash.ToHex(), hash.ToHex());

                try
                {
                    if (destPath.FileExists()) destPath.Delete();
                    _hashCache?.Purge(destPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Preflight: failed to clean up {Name}", archive.Name);
                }

                if (attempt < MaxDownloadRetries)
                    SetItemStatus(item, $"Hash mismatch — retrying ({attempt}/{MaxDownloadRetries})...", null);
            }

            // All retries exhausted
            SetItemStatus(item, $"Failed: hash mismatch after {MaxDownloadRetries} attempts", null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Preflight: download paused for {Name}", archive.Name);
            SetItemStatus(item, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preflight: download failed for {Name}", archive.Name);
            SetItemStatus(item, $"Failed: {ex.Message}", null);
        }
        finally
        {
            lock (_lock) { _activeDownloads.Remove(fileName); }
        }
    }

    private async Task<Hash> DownloadWithProgress(Archive archive, AbsolutePath destPath,
        PreflightSubItem item, string statusPrefix, CancellationToken token)
    {
        var expectedSize = archive.Size;
        var destFileInfo = new FileInfo(destPath.ToString());
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        _ = Task.Run(async () =>
        {
            while (!progressCts.Token.IsCancellationRequested)
            {
                await Task.Delay(ProgressPollIntervalMs, progressCts.Token)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (progressCts.Token.IsCancellationRequested) break;
                try
                {
                    destFileInfo.Refresh();
                    if (destFileInfo.Exists && expectedSize > 0)
                    {
                        var pct = Math.Min(1.0, (double)destFileInfo.Length / expectedSize);
                        SetItemStatus(item, $"{statusPrefix}Downloading...", pct);
                    }
                }
                catch { /* file may not exist yet or be locked */ }
            }
        }, progressCts.Token);

        var hash = await _downloadDispatcher!.Download(archive, destPath, token);
        await progressCts.CancelAsync();
        return hash;
    }

    private void SetItemStatus(PreflightSubItem item, string? status, double? progress)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            item.StatusText = status;
            item.Progress = progress;
        });
    }

    private void MarkItemCompleted(PreflightSubItem item)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            item.IsReady = true;
            item.StatusText = null;
            item.Progress = null;
            UpdateStatus(); // immediately rebuild SubItems so item disappears from the list
        });
    }

    public void PauseAutoDownload()
    {
        _autoDownloadCts?.Cancel();
    }

    /// <summary>
    /// Schedule an UpdateStatus call on the UI thread, debounced to at most once per second.
    /// </summary>
    private void ScheduleUpdateStatus()
    {
        if (Interlocked.CompareExchange(ref _updateStatusPending, 1, 0) != 0) return;
        Task.Run(async () =>
        {
            await Task.Delay(UpdateStatusDebounceMs);
            Interlocked.Exchange(ref _updateStatusPending, 0);
            RxApp.MainThreadScheduler.Schedule(UpdateStatus);
        });
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

        lock (_lock)
        {
            var match = _tracked.FirstOrDefault(t => !t.Item.IsReady && t.Archive.Hash == hash);
            if (match.Archive == null)
            {
                _logger.LogInformation("Preflight: hash {Hash} does not match any tracked archive", hash.ToHex());
                return;
            }

            _logger.LogInformation("Preflight: matched {File} to archive {Archive}", filePath.FileName, match.Archive.Name);

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

            MarkItemCompleted(match.Item);
        }
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
        var name = Path.GetFileName(fullPath);

        // Skip temp files and files currently being auto-downloaded
        if (name.EndsWith(".tmp") || name.EndsWith(".crdownload")) return;
        lock (_lock)
        {
            if (_activeDownloads.Contains(name)) return;
        }

        Task.Run(async () =>
        {
            await Task.Delay(WatcherSettleDelayMs, _lifetimeCts.Token)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (_lifetimeCts.Token.IsCancellationRequested) return;
            await TryMatchFile((AbsolutePath)fullPath, _lifetimeCts.Token);
        });
    }

    private void UpdateStatus()
    {
        lock (_lock)
        {
            var readyCount = _tracked.Count(t => t.Item.IsReady);
            var missingManualCount = _tracked.Count(t => t.Item is { IsReady: false } && IsManualDownload(t.Archive));
            var missingAutoCount = _tracked.Count(t => t.Item is { IsReady: false } && !IsManualDownload(t.Archive)
                                                       && (!t.NeedsPremium || IsPremium));
            var missingNexusNoPremium = _tracked.Count(t => t.Item is { IsReady: false } && t.NeedsPremium && !IsPremium);
            var allReady = readyCount == _tracked.Count;

            _logger.LogDebug("Preflight downloads status: {Ready}/{Total} ready, {Auto} auto-downloadable, {Manual} manual, {NexusNoPremium} nexus (no premium)",
                readyCount, _tracked.Count, missingAutoCount, missingManualCount, missingNexusNoPremium);

            // Only rebuild SubItems list when the count actually changes
            if (readyCount != ReadyCount)
            {
                SubItems = _tracked.Where(t => !t.Item.IsReady).OrderByDescending(t => t.Archive.Size).Select(t => t.Item).ToList();
            }

            ReadyCount = readyCount;
            TotalTracked = _tracked.Count;

            var readySuffix = readyCount > 0 ? $" ({readyCount} of {_tracked.Count} ready)" : "";

            if (allReady && _scanComplete)
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = $"All {_tracked.Count} files ready";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (allReady && !_scanComplete)
            {
                // All items matched so far, but scan is still running
                Status = PreflightCheckStatus.Checking;
                FailureMessage = $"Verifying files...{readySuffix}";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingAutoCount > 0)
            {
                // There are files we can auto-download — this is a blocking failure until downloaded
                Status = PreflightCheckStatus.Failed;
                FailureMessage = IsAutoDownloading
                    ? $"Downloading files...{readySuffix}"
                    : $"{missingAutoCount} files available for automatic download{readySuffix}";
                ActionCommand = IsAutoDownloading ? _pauseAutoDownloadCommand : _startAutoDownloadCommand;
                ActionLabel = IsAutoDownloading ? "Pause" : "Automatic Download";
            }
            else if (missingNexusNoPremium > 0)
            {
                // Only Nexus files remain but user isn't premium
                Status = PreflightCheckStatus.Failed;
                var total = missingNexusNoPremium + missingManualCount;
                FailureMessage = $"{total} files need downloading — Nexus files require premium{readySuffix}";
                ActionCommand = _getNexusPremiumCommand;
                ActionLabel = "Get Nexus Premium";
            }
            else
            {
                // Only manual downloads remain
                Status = PreflightCheckStatus.Failed;
                FailureMessage = $"{missingManualCount} files need manual download{readySuffix}";
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

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _autoDownloadCts?.Cancel();
        _disposable.Dispose();
        _lifetimeCts.Dispose();
    }
}
