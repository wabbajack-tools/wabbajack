using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Feeds the paths of files that may have changed in one folder into a channel. Three sources write to
///     it: an initial scan, a <see cref="FileSystemWatcher" /> (fast, but drops events under load, misses
///     files that were already there and is unreliable on synced or network folders) and a periodic poll,
///     which is the reliability floor. Watcher handlers only enqueue; every file system call happens on the
///     poll loop or in the consumer.
/// </summary>
internal sealed class WatchFolderSource : IAsyncDisposable
{
    private readonly AbsolutePath _folder;
    private readonly string _folderString;
    private readonly ManualDownloadAcquirerOptions _options;
    private readonly ChannelWriter<AbsolutePath> _writer;
    private readonly Func<FileInfo, bool> _interesting;
    private readonly Action<ManualDownloadNotice> _notify;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loop;

    private FileSystemWatcher? _watcher;
    private TaskCompletionSource _kick = NewKick();
    private volatile bool _watcherFaulted;
    private bool _unavailableNoticed;

    /// <param name="interesting">
    ///     Cheap filter applied to each file found by a poll. The <see cref="FileInfo" /> from the enumeration
    ///     already carries size and attributes, so no extra call per file is needed.
    /// </param>
    public WatchFolderSource(AbsolutePath folder, ManualDownloadAcquirerOptions options,
        ChannelWriter<AbsolutePath> writer, Func<FileInfo, bool> interesting,
        Action<ManualDownloadNotice> notify, ILogger logger, CancellationToken token)
    {
        _folder = folder;
        _folderString = folder.ToString();
        _options = options;
        _writer = writer;
        _interesting = interesting;
        _notify = notify;
        _logger = logger;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _loop = Task.Run(() => PollLoop(_cts.Token), CancellationToken.None);
    }

    public AbsolutePath Folder => _folder;

    /// <summary>Requests a scan now, without waiting for the next poll tick.</summary>
    public void Kick()
    {
        Volatile.Read(ref _kick).TrySetResult();
    }

    /// <summary>
    ///     Enumerates the folder on the calling thread, writing every interesting file to the channel. Returns
    ///     false when the folder is not there.
    /// </summary>
    public bool ScanNow()
    {
        if (!Directory.Exists(_folderString))
        {
            if (!_unavailableNoticed)
            {
                _unavailableNoticed = true;
                _notify(new ManualDownloadNotice(ManualDownloadNoticeKind.WatchFolderUnavailable, _folder,
                    $"The folder {_folder} does not exist. Waiting for it to appear; pick another folder to watch instead."));
            }

            return false;
        }

        _unavailableNoticed = false;

        try
        {
            foreach (var file in new DirectoryInfo(_folderString).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                bool wanted;
                try
                {
                    wanted = _interesting(file);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping {File} in watch folder", file.FullName);
                    continue;
                }

                if (wanted)
                    _writer.TryWrite(file.FullName.ToAbsolutePath());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not enumerate {Folder}", _folder);
            return false;
        }

        return true;
    }

    private async Task PollLoop(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        Task<bool>? tick = null;

        try
        {
            ScanOnce(token);

            while (!token.IsCancellationRequested)
            {
                tick ??= timer.WaitForNextTickAsync(token).AsTask();
                var kick = Volatile.Read(ref _kick);

                var finished = await Task.WhenAny(tick, kick.Task);
                if (finished == tick)
                {
                    if (!await tick) break;
                    tick = null;
                }
                else
                {
                    // The fired source is replaced before scanning, so a kick that arrives during the scan
                    // lands on the fresh one and causes one more pass.
                    Interlocked.CompareExchange(ref _kick, NewKick(), kick);
                }

                ScanOnce(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watch folder poll loop for {Folder} stopped", _folder);
        }
        finally
        {
            DisposeWatcher();
        }
    }

    private void ScanOnce(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_watcherFaulted)
        {
            _watcherFaulted = false;
            DisposeWatcher();
        }

        var available = ScanNow();

        if (!available)
        {
            DisposeWatcher();
            return;
        }

        if (_options.UseFileSystemWatcher && _watcher == null)
            CreateWatcher();
    }

    private void CreateWatcher()
    {
        try
        {
            var watcher = new FileSystemWatcher(_folderString)
            {
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite
            };
            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not watch {Folder}; relying on polling", _folder);
            _notify(new ManualDownloadNotice(ManualDownloadNoticeKind.WatcherError, _folder,
                $"Change notifications for {_folder} are unavailable ({ex.Message}); the folder is polled instead."));
            _watcher = null;
        }
    }

    private void DisposeWatcher()
    {
        var watcher = Interlocked.Exchange(ref _watcher, null);
        if (watcher == null) return;
        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Disposing watcher for {Folder}", _folder);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Enqueue(e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Enqueue(e.FullPath);
        if (!string.IsNullOrEmpty(e.OldFullPath))
            Enqueue(e.OldFullPath);
    }

    /// <summary>
    ///     A browser writing a <c>.crdownload</c> raises a change per chunk; the extension check is a pure
    ///     string test, so it runs here rather than making each of those a channel item.
    /// </summary>
    private void Enqueue(string fullPath)
    {
        var path = fullPath.ToAbsolutePath();
        if (CandidateFile.IsPartialDownload(path, _options.PartialExtensions)) return;
        _writer.TryWrite(path);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Usually the internal buffer overflowed, which means events were lost. The next scan picks up
        // whatever was missed and recreates the watcher; nothing is done on this thread.
        var ex = e.GetException();
        _logger.LogWarning(ex, "Watcher for {Folder} reported an error; rescanning", _folder);
        _watcherFaulted = true;
        _notify(new ManualDownloadNotice(ManualDownloadNoticeKind.WatcherError, _folder,
            $"Change notifications for {_folder} were interrupted ({ex?.Message}); the folder was rescanned."));
        Kick();
    }

    private static TaskCompletionSource NewKick()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        DisposeWatcher();
        _cts.Dispose();
    }
}
