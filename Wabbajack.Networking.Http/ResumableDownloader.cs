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
            return await DownloadStreamDirectlyToFile(_msg.RequestUri, token, _outputPath, 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download '{name}'", _outputPath.FileName.ToString());

            throw;
        }
    }

    private async Task<Hash> DownloadStreamDirectlyToFile(Uri rquestURI, CancellationToken token, AbsolutePath filePath, int retry = 5)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("SmallFilesClient");
            using Stream fileStream = GetFileStream(filePath);
            var startingPosition = fileStream.Length;

            _logger.LogDebug("Download for '{name}' is starting from {position}...", filePath.FileName.ToString(), startingPosition);
            httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(startingPosition, null); //GetStreamAsync does not accept a HttpRequestMessage so we have to set headers on the client itself

            var response = await httpClient.GetStreamAsync(rquestURI, token);
            await response.CopyToAsync(fileStream, token);

            return await fileStream.Hash(token);
        }
        catch (Exception ex) when (ex is SocketException || ex is IOException)
        {
            _logger.LogWarning(ex, "Failed to download '{name}' due to network error. Retrying...", filePath.FileName.ToString());

            if (retry == 0)
            {
                throw;
            }

            return await DownloadStreamDirectlyToFile(rquestURI, token, filePath, retry--);
        }
    }

    private Stream GetFileStream(AbsolutePath filePath)
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