using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Humanizer;
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

    /// <summary>Total bytes of archives confirmed present in the download folder.</summary>
    public long PresentArchiveSize { get; private set; }

    public DownloadsCheck(IReadOnlyList<Archive> allArchives, AbsolutePath downloadDir,
        AbsolutePath systemDownloadsDir, bool isPremium)
    {
        _downloadDir = downloadDir;
        _isPremium = isPremium;

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
        if (!_downloadDir.DirectoryExists()) return;

        foreach (var file in _downloadDir.EnumerateFiles())
        {
            await TryMatchFile(file, token);
        }
    }

    private async Task TryMatchFile(AbsolutePath filePath, CancellationToken token)
    {
        if (!filePath.FileExists()) return;

        long fileSize;
        try { fileSize = filePath.Size(); }
        catch { return; }

        List<(Archive Archive, PreflightSubItem Item, bool IsNexus)> candidates;
        lock (_lock)
        {
            candidates = _tracked
                .Where(t => !t.Item.IsReady && t.Archive.Size == fileSize)
                .ToList();
        }

        if (candidates.Count == 0) return;

        // Show verification progress on the first candidate's sub-item
        var subItem = candidates[0].Item;
        var fileName = filePath.FileName.ToString();
        subItem.StatusText = $"Verifying {fileName}...";
        subItem.Progress = 0;

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
                    subItem.Progress = (double)totalRead / fileSize;
            }
            hash = new Hash(hasher.GetCurrentHashAsUInt64());
        }
        catch
        {
            subItem.StatusText = null;
            subItem.Progress = null;
            return;
        }

        subItem.StatusText = null;
        subItem.Progress = null;

        // Find which tracked archive matches this hash
        lock (_lock)
        {
            var match = _tracked.FirstOrDefault(t => !t.Item.IsReady && t.Archive.Hash == hash);
            if (match.Archive == null) return;

            // Move file to download dir if not already there
            var destPath = _downloadDir.Combine(match.Archive.Name);
            if (filePath != destPath)
            {
                try
                {
                    // Ensure the download directory exists
                    if (!_downloadDir.DirectoryExists())
                        _downloadDir.CreateDirectory();

                    if (destPath.FileExists()) destPath.Delete();
                    File.Move(filePath.ToString(), destPath.ToString());
                }
                catch
                {
                    try { File.Copy(filePath.ToString(), destPath.ToString(), true); }
                    catch { return; }
                }
            }

            match.Item.IsReady = true;
            match.Item.StatusText = "Ready";
            _matchedHashes.Add(hash);
            PresentArchiveSize += match.Archive.Size;
        }

        UpdateStatus();
    }

    private void StartWatching(AbsolutePath downloadDir, AbsolutePath systemDownloadsDir)
    {
        WatchDirectory(downloadDir);
        if (systemDownloadsDir != downloadDir && systemDownloadsDir != default)
            WatchDirectory(systemDownloadsDir);
    }

    private void WatchDirectory(AbsolutePath dir)
    {
        if (dir == default) return;

        // Create the directory if it doesn't exist so the watcher works
        if (!dir.DirectoryExists())
            dir.CreateDirectory();

        var watcher = new FileSystemWatcher(dir.ToString())
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        watcher.Created += (_, e) => OnFileChanged(e.FullPath);
        watcher.Changed += (_, e) => OnFileChanged(e.FullPath);
        watcher.Renamed += (_, e) => OnFileChanged(e.FullPath);

        _disposable.Add(watcher);
    }

    private void OnFileChanged(string fullPath)
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            await TryMatchFile((AbsolutePath)fullPath, CancellationToken.None);
        });
    }

    private void UpdateStatus()
    {
        lock (_lock)
        {
            var missingManual = _tracked.Where(t => !t.IsNexus && !t.Item.IsReady).ToList();
            var missingNexus = _tracked.Where(t => t.IsNexus && !t.Item.IsReady).ToList();
            var allReady = _tracked.All(t => t.Item.IsReady);

            if (allReady)
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = null;
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingManual.Count == 0 && missingNexus.Count > 0 && _isPremium)
            {
                // Only Nexus files missing, premium user — non-blocking
                Status = PreflightCheckStatus.Info;
                FailureMessage = $"{missingNexus.Count} Nexus files will be downloaded automatically during install";
                ActionCommand = null;
                ActionLabel = null;
            }
            else if (missingNexus.Count > 0 && !_isPremium)
            {
                Status = PreflightCheckStatus.Failed;
                var total = missingNexus.Count + missingManual.Count;
                FailureMessage = missingManual.Count > 0
                    ? $"{total} files need downloading"
                    : $"{missingNexus.Count} Nexus files require premium or manual download";
                ActionCommand = ReactiveCommand.Create(() =>
                    OpenUrl("https://next.nexusmods.com/premium"));
                ActionLabel = "Get Nexus Premium";
            }
            else
            {
                Status = PreflightCheckStatus.Failed;
                FailureMessage = "Download these files — they'll be detected automatically";
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
