using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Http;

public class ResumableDownloader(ILogger<ResumableDownloader> _logger, IHttpClientFactory _httpClientFactory) : IHttpDownloader
{
    public async Task<Hash> Download(HttpRequestMessage _msg, AbsolutePath _outputPath, IJob job, CancellationToken token)
    {
        if (_msg.RequestUri == null)
        {
            throw new ArgumentException("Request URI is null");
        }

        try
        {
            return await DownloadAndHash(_msg, _outputPath, job, token, 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download '{name}'", _outputPath.FileName.ToString());

            throw;
        }
    }

    private async Task<Hash> DownloadAndHash(HttpRequestMessage msg, AbsolutePath filePath, IJob job, CancellationToken token, int retry = 5, bool reset = false)
    {
        try
        {
            if (reset)
            {
                filePath.Delete();
            }

            var downloadedFilePath = await DownloadStreamDirectlyToFile(msg, filePath, job, token);

            return await HashFile(downloadedFilePath, token);
        }
        catch (IOException ex) when (IOErrors.IsDiskFull(ex))
        {
            _logger.LogError(ex, "Not enough disk space to download '{name}'", filePath.FileName.ToString());
            throw;
        }
        catch (Exception ex) when (ex is SocketException || ex is IOException)
        {
            _logger.LogWarning(ex, "Failed to download '{name}' due to network error. Retrying...", filePath.FileName.ToString());

            if (retry == 0)
            {
                _logger.LogError(ex, "Failed to download '{name}'", filePath.FileName.ToString());

                throw;
            }

            return await DownloadAndHash(CloneRequest(msg), filePath, job, token, retry - 1);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            _logger.LogWarning(ex, "Failed to download '{name}' due to requested range not being satisfiable. Retrying from beginning...", filePath.FileName.ToString());

            if (retry == 0)
            {
                _logger.LogError(ex, "Failed to download '{name}'", filePath.FileName.ToString());

                throw;
            }

            return await DownloadAndHash(CloneRequest(msg), filePath, job, token, retry - 1, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download '{name}'", filePath.FileName.ToString());

            throw;
        }
    }

    private async Task<AbsolutePath> DownloadStreamDirectlyToFile(HttpRequestMessage message, AbsolutePath filePath, IJob job, CancellationToken token)
    {
        if(job.Size == null) throw new ArgumentException("Job size must be set before downloading");

        // What is on disk is what has been received: the file is never extended ahead of the bytes, so after a
        // dropped connection its length is exactly where the next attempt resumes from.
        var startingPosition = filePath.FileExists() ? filePath.Size() : 0;

        _logger.LogDebug("Download for '{name}' is starting from {position}...", filePath.FileName.ToString(), startingPosition);

        var httpClient = _httpClientFactory.CreateClient("ResumableClient");

        message.Headers.Range = new RangeHeaderValue(startingPosition, null);
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

        // An error page is not the file. A 416 on a resume is caught above and starts over.
        response.EnsureSuccessStatusCode();

        // Only a 206 starting where the file ends continues it. A server that ignores the range sends the whole
        // file with a 200, which has to replace what is there rather than follow it.
        var resumes = startingPosition > 0 &&
                      response.StatusCode == System.Net.HttpStatusCode.PartialContent &&
                      response.Content.Headers.ContentRange?.From == startingPosition;
        if (startingPosition > 0 && !resumes)
        {
            _logger.LogDebug("Server did not resume '{name}' from {position}, starting over", filePath.FileName.ToString(), startingPosition);
            startingPosition = 0;
        }

        using Stream fileStream = resumes
            ? filePath.Open(FileMode.Append, FileAccess.Write, FileShare.None)
            : filePath.Open(FileMode.Create, FileAccess.Write, FileShare.None);

        var responseStream = await response.Content.ReadAsStreamAsync(token);

        long reportProgressThreshold = 10 * 1024 * 1024; // Report progress every 100MB
        bool shouldReportProgress = job.Size > reportProgressThreshold; // Reporting progress on small files just generates strain on the UI

        int reportEveryXBytesProcessed = (int) job.Size / 100; // Report progress every 1% of the file
        long bytesProcessed = startingPosition;

        var buffer = new byte[128 * 1024];
        int bytesRead;
        while ((bytesRead = await responseStream.ReadAsync(buffer, token)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
            bytesProcessed += bytesRead;

            if (shouldReportProgress && bytesProcessed >= reportEveryXBytesProcessed)
            {
                job.ReportNoWait((int)bytesProcessed);
                bytesProcessed = 0;
            }
        }

        return filePath;
    }

    private static async Task<Hash> HashFile(AbsolutePath filePath, CancellationToken token)
    {
        using var fileStream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        return await fileStream.Hash(token);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        clone.Version = original.Version;

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}