using System;
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

namespace Wabbajack.Preflight;

public partial class DownloadsCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();
    private readonly AbsolutePath _downloadDir;
    private readonly ILogger _logger;
    private readonly DownloadDispatcher? _downloadDispatcher;
    [Reactive] public partial bool IsPremium { get; set; }

    // All tracked archives with their sub-items
    private readonly List<(Archive Archive, PreflightSubItem Item, bool IsNexus)> _tracked = new();
    private readonly HashSet<Hash> _matchedHashes = new();
    private readonly object _lock = new();

    // Auto-download state
    private CancellationTokenSource? _autoDownloadCts;
    [Reactive] public partial bool IsAutoDownloading { get; set; }

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
        DownloadDispatcher? downloadDispatcher = null)
    {
        _downloadDir = downloadDir;
        IsPremium = isPremium;
        _logger = logger;
        _downloadDispatcher = downloadDispatcher;

        // Re-evaluate status when premium state changes
        Observable.Skip(this.WhenAnyValue(x => x.IsPremium), 1)
            .Subscribe(_ =>
            {
                _logger.LogInformation("Preflight: premium status changed to {IsPremium}", IsPremium);
                UpdateStatus();
            })
            .DisposeWith(_disposable);

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
                SizeBytes = a.Size,
                SizeText = a.Size.Bytes().ToString(),
                ActionCommand = string.IsNullOrEmpty(url) ? null : ReactiveCommand.Create(() => OpenUrl(url)),
                ActionLabel = string.IsNullOrEmpty(url) ? null : "Download"
            };

            _tracked.Add((a, item, isNexus));
        }

        _logger.LogInformation("Preflight downloads: tracking {Count} archives ({Nexus} Nexus, {Manual} manual)",
            _tracked.Count, _tracked.Count(t => t.IsNexus), _tracked.Count(t => !t.IsNexus));

        SubItems = _tracked.Where(t => !t.Item.IsReady).OrderByDescending(t => t.Archive.Size).Select(t => t.Item).ToList();
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

    /// <summary>
    /// Start auto-downloading all missing Nexus archives. Premium only.
    /// </summary>
    public void StartAutoDownload()
    {
        if (!IsPremium || _downloadDispatcher == null || IsAutoDownloading) return;

        IsAutoDownloading = true;
        _autoDownloadCts = new CancellationTokenSource();
        var token = _autoDownloadCts.Token;

        if (!_downloadDir.DirectoryExists())
            _downloadDir.CreateDirectory();

        // Get all undownloaded Nexus archives
        List<(Archive Archive, PreflightSubItem Item, bool IsNexus)> toDownload;
        lock (_lock)
        {
            toDownload = _tracked.Where(t => t.IsNexus && !t.Item.IsReady).ToList();
        }

        _logger.LogInformation("Preflight: starting parallel auto-download of {Count} Nexus archives", toDownload.Count);

        // Fire all downloads concurrently — the DownloadDispatcher's IResource limiter
        // gates how many actually run in parallel based on ResourceSettings.MaxTasks
        var tasks = toDownload.Select(entry => DownloadOneAsync(entry.Archive, entry.Item, token));
        Task.Run(async () =>
        {
            await Task.WhenAll(tasks);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                IsAutoDownloading = false;
                UpdateStatus();
            });
        }, token);
    }

    private async Task DownloadOneAsync(Archive archive, PreflightSubItem item, CancellationToken token)
    {
        var destPath = _downloadDir.Combine(archive.Name);
        _logger.LogInformation("Preflight: auto-downloading {Name}", archive.Name);

        RxApp.MainThreadScheduler.Schedule(() =>
        {
            item.StatusText = "Downloading...";
            item.Progress = 0;
        });

        try
        {
            // Poll file size for progress while downloading
            var expectedSize = archive.Size;
            var destFileInfo = new FileInfo(destPath.ToString());
            var progressCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var progressTask = Task.Run(async () =>
            {
                while (!progressCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(2000, progressCts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    try
                    {
                        destFileInfo.Refresh();
                        if (destFileInfo.Exists && expectedSize > 0)
                        {
                            var pct = Math.Min(1.0, (double)destFileInfo.Length / expectedSize);
                            RxApp.MainThreadScheduler.Schedule(() => item.Progress = pct);
                        }
                    }
                    catch { /* file may not exist yet or be locked */ }
                }
            }, progressCts.Token);

            var hash = await _downloadDispatcher!.Download(archive, destPath, token);
            await progressCts.CancelAsync();

            if (hash == archive.Hash)
            {
                _logger.LogInformation("Preflight: auto-download complete: {Name}", archive.Name);
                lock (_lock)
                {
                    _matchedHashes.Add(hash);
                    PresentArchiveSize += archive.Size;
                }

                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    item.IsReady = true;
                    item.StatusText = null;
                    item.Progress = null;
                });
                RxApp.MainThreadScheduler.Schedule(UpdateStatus);
            }
            else
            {
                _logger.LogWarning("Preflight: hash mismatch for {Name}: expected {Expected}, got {Actual}",
                    archive.Name, archive.Hash.ToHex(), hash.ToHex());
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    item.StatusText = "Hash mismatch";
                    item.Progress = null;
                });
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Preflight: auto-download paused for {Name}", archive.Name);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                item.StatusText = null;
                item.Progress = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preflight: auto-download failed for {Name}", archive.Name);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                item.StatusText = $"Failed: {ex.Message}";
                item.Progress = null;
            });
        }
    }

    public void PauseAutoDownload()
    {
        _autoDownloadCts?.Cancel();
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
            await Task.Delay(2000);
            await TryMatchFile((AbsolutePath)fullPath, CancellationToken.None);
        });
    }

    private void UpdateStatus()
    {
        lock (_lock)
        {
            var readyCount = _tracked.Count(t => t.Item.IsReady);
            var missingManualCount = _tracked.Count(t => !t.IsNexus && !t.Item.IsReady);
            var missingNexusCount = _tracked.Count(t => t.IsNexus && !t.Item.IsReady);
            var allReady = readyCount == _tracked.Count;

            _logger.LogDebug("Preflight downloads status: {Ready}/{Total} ready, {MissingManual} manual missing, {MissingNexus} nexus missing",
                readyCount, _tracked.Count, missingManualCount, missingNexusCount);

            // Only rebuild SubItems list when the count actually changes
            if (readyCount != ReadyCount)
            {
                SubItems = _tracked.Where(t => !t.Item.IsReady).OrderByDescending(t => t.Archive.Size).Select(t => t.Item).ToList();
            }

            ReadyCount = readyCount;
            TotalTracked = _tracked.Count;

            var readySuffix = readyCount > 0 ? $" ({readyCount} of {_tracked.Count} ready)" : "";

            if (allReady)
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = $"All {_tracked.Count} files ready";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingManualCount == 0 && missingNexusCount > 0 && IsPremium)
            {
                Status = PreflightCheckStatus.Info;
                FailureMessage = IsAutoDownloading
                    ? $"Downloading Nexus files...{readySuffix}"
                    : $"{missingNexusCount} Nexus files available for automatic download{readySuffix}";
                ActionCommand = IsAutoDownloading
                    ? ReactiveCommand.Create(PauseAutoDownload)
                    : ReactiveCommand.Create(StartAutoDownload);
                ActionLabel = IsAutoDownloading ? "Pause" : "Automatic Download";
            }
            else if (missingNexusCount > 0 && !IsPremium)
            {
                Status = PreflightCheckStatus.Failed;
                var total = missingNexusCount + missingManualCount;
                FailureMessage = missingManualCount > 0
                    ? $"{total} files need downloading{readySuffix}"
                    : $"{missingNexusCount} Nexus files require premium or manual download{readySuffix}";
                ActionCommand = ReactiveCommand.Create(() =>
                    OpenUrl("https://next.nexusmods.com/premium"));
                ActionLabel = "Get Nexus Premium";
            }
            else
            {
                Status = PreflightCheckStatus.Failed;
                FailureMessage = $"Download these files — they'll be detected automatically{readySuffix}";
                // If there are also Nexus files and we're premium, show the auto-download button
                if (missingNexusCount > 0 && IsPremium)
                {
                    ActionCommand = IsAutoDownloading
                        ? ReactiveCommand.Create(PauseAutoDownload)
                        : ReactiveCommand.Create(StartAutoDownload);
                    ActionLabel = IsAutoDownloading ? "Pause" : "Automatic Download";
                }
                else
                {
                    ActionCommand = null;
                    ActionLabel = null;
                }
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
        _autoDownloadCts?.Cancel();
        _disposable.Dispose();
    }
}
