using Microsoft.Extensions.Logging;
using Wabbajack.Configuration;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloader.Clients;

public interface IDownloadClientFactory
{
    public IDownloadClient GetDownloader(HttpRequestMessage msg, AbsolutePath outputPath, IJob job);
}

public class DownloadClientFactory(PerformanceSettings _performanceSettings, ILoggerFactory _loggerFactory, IHttpClientFactory _httpClientFactory) : IDownloadClientFactory
{
    private readonly ILogger<NonResumableDownloadClient> _nonResuableDownloaderLogger = _loggerFactory.CreateLogger<NonResumableDownloadClient>();
    private readonly ILogger<ResumableDownloadClient> _resumableDownloaderLogger = _loggerFactory.CreateLogger<ResumableDownloadClient>();

    private NonResumableDownloadClient? _nonReusableDownloader = default;

    public IDownloadClient GetDownloader(HttpRequestMessage msg, AbsolutePath outputPath, IJob job)
    {
        if (job.Size >= _performanceSettings.MinimumFileSizeForResumableDownload)
        {
            return new ResumableDownloadClient(msg, outputPath, job, _performanceSettings, _resumableDownloaderLogger);
        }
        else
        {
            _nonReusableDownloader ??= new NonResumableDownloadClient(msg, outputPath, _nonResuableDownloaderLogger, _httpClientFactory);

            return new NonResumableDownloadClient(msg, outputPath, _nonResuableDownloaderLogger, _httpClientFactory);
        }
    }
}
