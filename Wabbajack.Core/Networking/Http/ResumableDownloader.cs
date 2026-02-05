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
        catch (Exception ex) when (ex is SocketException || ex is IOException)
        {
            _logger.LogWarning(ex, "Failed to download '{name}' due to network error. Retrying...", filePath.FileName.ToString());

            if (retry == 0)
            {
                _logger.LogError(ex, "Failed to download '{name}'", filePath.FileName.ToString());

                throw;
            }

            return await DownloadAndHash(msg, filePath, job, token, retry--);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            _logger.LogWarning(ex, "Failed to download '{name}' due to requested range not being satisfiable. Retrying from beginning...", filePath.FileName.ToString());

            if (retry == 0)
            {
                _logger.LogError(ex, "Failed to download '{name}'", filePath.FileName.ToString());

                throw;
            }

            return await DownloadAndHash(msg, filePath, job, token, retry--, true);
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

        using Stream fileStream = GetDownloadFileStream(filePath);

        var startingPosition = fileStream.Length;

        _logger.LogDebug("Download for '{name}' is starting from {position}...", filePath.FileName.ToString(), startingPosition);

        var httpClient = _httpClientFactory.CreateClient("ResumableClient");

        message.Headers.Range = new RangeHeaderValue(startingPosition, null);
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

        var responseContentLength = response.Content.Headers.ContentLength;

        if (responseContentLength != null && responseContentLength > fileStream.Length)
        {
            fileStream.SetLength(responseContentLength.Value);
        }

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

    private static Stream GetDownloadFileStream(AbsolutePath filePath)
    {
        if (filePath.FileExists())
        {
            return filePath.Open(FileMode.Append, FileAccess.Write, FileShare.None);
        }
        else
        {
            return filePath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        }
    }
}