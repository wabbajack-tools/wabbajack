using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloader.Clients;

public class DownloaderService(ILogger<DownloaderService> _logger, IDownloadClientFactory _httpDownloaderFactory) : IHttpDownloader
{
    public async Task<Hash> Download(HttpRequestMessage message, AbsolutePath outputPath, IJob job,
        CancellationToken token)
    {
        Exception downloadError = null!;

        var downloader = _httpDownloaderFactory.GetDownloader(message, outputPath, job);

        for (var i = 0; i < 3; i++)
        {
            try
            {
                return await downloader.Download(token, 3);
            }
            catch (Exception ex)
            {
                downloadError = ex;
                _logger.LogDebug("Download for '{name}' failed. Retrying...", outputPath.FileName.ToString());
            }
        }

        _logger.LogError(downloadError, "Failed to download '{name}' after 3 tries.", outputPath.FileName.ToString());
        return new Hash();


    }
}