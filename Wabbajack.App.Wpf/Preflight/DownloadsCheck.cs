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

    // Missing archives split by category
    private readonly Dictionary<Hash, Archive> _missingManual;
    private readonly Dictionary<Hash, Archive> _missingNexus;
    private readonly HashSet<Hash> _presentHashes = new();
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

        // Split missing archives into Nexus vs manual
        _missingNexus = allArchives
            .Where(a => a.State is Nexus)
            .ToDictionary(a => a.Hash, a => a);

        _missingManual = allArchives
            .Where(a => IsManualDownload(a))
            .ToDictionary(a => a.Hash, a => a);

        UpdateStatus();
        StartWatching(downloadDir, systemDownloadsDir);
    }

    /// <summary>
    /// Returns true if the archive requires manual download (not Nexus, not HTTP, not GameFileSource).
    /// </summary>
    private static bool IsManualDownload(Archive archive)
    {
        return archive.State switch
        {
            Http => false,
            GameFileSource => false,
            Nexus => false, // Nexus tracked separately
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

        var fileSize = filePath.Size();

        List<Archive> candidates;
        lock (_lock)
        {
            candidates = _missingManual.Values.Concat(_missingNexus.Values)
                .Where(a => a.Size == fileSize).ToList();
        }

        if (candidates.Count == 0) return;

        PreflightSubItem? subItem = null;
        var fileName = filePath.FileName.ToString();
        lock (_lock)
        {
            subItem = SubItems?.FirstOrDefault(s => candidates.Any(c => c.Name == s.Name));
        }

        if (subItem != null)
        {
            subItem.StatusText = $"Verifying {fileName}...";
            subItem.Progress = 0;
        }

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
                if (subItem != null && fileSize > 0)
                    subItem.Progress = (double)totalRead / fileSize;
            }
            hash = new Hash(hasher.GetCurrentHashAsUInt64());
        }
        catch
        {
            if (subItem != null)
            {
                subItem.StatusText = null;
                subItem.Progress = null;
            }
            return;
        }

        if (subItem != null)
        {
            subItem.StatusText = null;
            subItem.Progress = null;
        }

        lock (_lock)
        {
            Archive? archive = null;
            if (_missingManual.TryGetValue(hash, out var manualArchive))
            {
                archive = manualArchive;
                _missingManual.Remove(hash);
            }
            else if (_missingNexus.TryGetValue(hash, out var nexusArchive))
            {
                archive = nexusArchive;
                _missingNexus.Remove(hash);
            }

            if (archive == null) return;

            var destPath = _downloadDir.Combine(archive.Name);
            if (filePath != destPath)
            {
                try
                {
                    if (destPath.FileExists()) destPath.Delete();
                    File.Move(filePath.ToString(), destPath.ToString());
                }
                catch
                {
                    try
                    {
                        File.Copy(filePath.ToString(), destPath.ToString(), true);
                    }
                    catch { return; }
                }
            }

            _presentHashes.Add(hash);
            PresentArchiveSize += archive.Size;
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
        if (!dir.DirectoryExists()) return;

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
            var hasManual = _missingManual.Count > 0;
            var hasNexus = _missingNexus.Count > 0;
            var hasNonPremiumNexus = hasNexus && !_isPremium;

            // Build sub-items list: Nexus archives first, then manual
            var items = new List<PreflightSubItem>();

            foreach (var a in _missingNexus.Values)
            {
                items.Add(new PreflightSubItem
                {
                    Name = a.Name,
                    SizeText = a.Size.Bytes().ToString(),
                    ActionCommand = ReactiveCommand.Create(() => OpenUrl(GetDownloadUrl(a))),
                    ActionLabel = "Open"
                });
            }

            foreach (var a in _missingManual.Values)
            {
                items.Add(new PreflightSubItem
                {
                    Name = a.Name,
                    SizeText = a.Size.Bytes().ToString(),
                    ActionCommand = ReactiveCommand.Create(() => OpenUrl(GetDownloadUrl(a))),
                    ActionLabel = "Download"
                });
            }

            if (!hasManual && !hasNexus)
            {
                // Everything present
                Status = PreflightCheckStatus.Passed;
                FailureMessage = null;
                ActionCommand = null;
                ActionLabel = null;
                SubItems = null;
            }
            else if (!hasManual && hasNexus && _isPremium)
            {
                // Only Nexus files missing, premium user — non-blocking, will auto-download
                Status = PreflightCheckStatus.Info;
                FailureMessage = $"{_missingNexus.Count} Nexus files will be downloaded automatically during install";
                ActionCommand = null; // Auto-download happens during install
                ActionLabel = null;
                SubItems = items;
            }
            else if (hasNexus && !_isPremium)
            {
                // Non-premium user with Nexus files — blocking
                Status = PreflightCheckStatus.Failed;
                FailureMessage = hasManual
                    ? $"{_missingNexus.Count + _missingManual.Count} files need downloading"
                    : $"{_missingNexus.Count} Nexus files require premium or manual download";
                ActionCommand = ReactiveCommand.Create(() =>
                    OpenUrl("https://next.nexusmods.com/premium"));
                ActionLabel = "Get Nexus Premium";
                SubItems = items;
            }
            else
            {
                // Manual files missing (possibly with Nexus files too)
                Status = PreflightCheckStatus.Failed;
                FailureMessage = "Download these files — they'll be detected automatically";
                if (hasNexus && _isPremium)
                {
                    ActionCommand = null;
                    ActionLabel = null;
                }
                else
                {
                    ActionCommand = null;
                    ActionLabel = null;
                }
                SubItems = items;
            }
        }
    }

    private static void OpenUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string GetDownloadUrl(Archive archive)
    {
        return archive.State switch
        {
            Manual manual => manual.Url.ToString(),
            MediaFire mediaFire => mediaFire.Url.ToString(),
            Mega mega => mega.Url.ToString(),
            IPS4OAuth2 ips4 => ips4.LinkUrl?.ToString() ?? string.Empty,
            GoogleDrive gd => gd.GetUri().ToString(),
            Http http => http.Url.ToString(),
            Nexus nexus => $"{nexus.LinkUrl}?tab=files&file_id={nexus.FileID}",
            _ => string.Empty
        };
    }

    public void Dispose() => _disposable.Dispose();
}
