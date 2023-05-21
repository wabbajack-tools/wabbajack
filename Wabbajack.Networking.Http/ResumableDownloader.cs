using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Downloader;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Http;

internal class ResumableDownloader
{
    private readonly IJob _job;
    private readonly HttpRequestMessage _msg;
    private readonly AbsolutePath _outputPath;
    private readonly AbsolutePath _packagePath;
    private readonly ILogger<SingleThreadedDownloader> _logger;
    private CancellationToken _token;
    private Exception? _error;


    public ResumableDownloader(HttpRequestMessage msg, AbsolutePath outputPath, IJob job, ILogger<SingleThreadedDownloader> logger)
    {
        _job = job;
        _msg = msg;
        _outputPath = outputPath;
        _packagePath = outputPath.WithExtension(Extension.FromPath(".download_package"));
        _logger = logger;
    }

    public async Task<Hash> Download(CancellationToken token)
    {
        _token = token;

        var downloader = new DownloadService(CreateConfiguration(_msg));
        downloader.DownloadStarted += OnDownloadStarted;
        downloader.DownloadProgressChanged += OnDownloadProgressChanged;
        downloader.DownloadFileCompleted += OnDownloadFileCompleted;

        // Attempt to resume previous download
        var downloadPackage = LoadPackage();
        if (downloadPackage != null)
        {
            // Resume with different Uri in case old one is no longer valid
            downloadPackage.Address = _msg.RequestUri!.AbsoluteUri;

            _logger.LogDebug("Download for {name} is resuming...", _outputPath.FileName.ToString());
            await downloader.DownloadFileTaskAsync(downloadPackage, token);
        }
        else
        {
            _logger.LogDebug("Download for '{name}' is starting from scratch...", _outputPath.FileName.ToString());
            _outputPath.Delete();
            await downloader.DownloadFileTaskAsync(_msg.RequestUri!.AbsoluteUri, _outputPath.ToString(), token);
        }

        // Save progress if download isn't completed yet
        if (downloader.Status is DownloadStatus.Stopped or DownloadStatus.Failed)
        {
            _logger.LogDebug("Download for '{name}' stopped before completion. Saving package...", _outputPath.FileName.ToString());
            SavePackage(downloader.Package);
            if (_error == null || _error.GetType() == typeof(TaskCanceledException))
            {
                return new Hash();
            }

            if (_error.GetType() == typeof(NotSupportedException))
            {
                _logger.LogWarning("Download for '{name}' doesn't support resuming. Deleting package...", _outputPath.FileName.ToString());
                DeletePackage();
            }
            else
            {
                _logger.LogError(_error,"Download for '{name}' encountered error. Throwing...", _outputPath.FileName.ToString());
            }

            throw _error;
        }

        if (downloader.Status == DownloadStatus.Completed)
        {
            DeletePackage();
        }

        if (!_outputPath.FileExists())
        {
            return new Hash();
        }

        await using var file = _outputPath.Open(FileMode.Open);
        return await file.Hash(token);
    }

    private DownloadConfiguration CreateConfiguration(HttpRequestMessage message)
    {
        var configuration = new DownloadConfiguration
        {
            Timeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds,
            ReserveStorageSpaceBeforeStartingDownload = true,
            RequestConfiguration = new RequestConfiguration
            {
                Headers = message.Headers.ToWebHeaderCollection(),
                ProtocolVersion = message.Version,
                UserAgent =  message.Headers.UserAgent.ToString()
            }
        };

        return configuration;
    }

    private void OnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        _error = e.Error;
    }

    private async void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        var processedSize = e.ProgressedByteSize;
        if (_job.Current == 0)
        {
            // Set current to total in case this download resumes from a previous one
            processedSize = e.ReceivedBytesSize;
        }

        await _job.Report(processedSize, _token);
        if (_job.Current > _job.Size)
        {
            // Increase job size so progress doesn't appear stalled
            _job.Size = (long)Math.Floor(_job.Current * 1.1);
        }
    }

    private void OnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        _job.ResetProgress();

        if (_job.Size < e.TotalBytesToReceive)
        {
            _job.Size = e.TotalBytesToReceive;
        }

        // Get rid of package, since we can't use it to resume anymore
        DeletePackage();
    }

    private void DeletePackage()
    {
        _packagePath.Delete();
    }

    private DownloadPackage? LoadPackage()
    {
        if (!_packagePath.FileExists())
        {
            return null;
        }

        var packageJson = _packagePath.ReadAllText();
        return JsonSerializer.Deserialize<DownloadPackage>(packageJson);
    }

    private void SavePackage(DownloadPackage package)
    {
        var packageJson = JsonSerializer.Serialize(package);
        _packagePath.WriteAllText(packageJson);
    }
}