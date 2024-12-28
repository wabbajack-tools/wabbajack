using Microsoft.Extensions.Logging;
using Wabbajack.Configuration;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloader.Services;

public interface IDownloadClientFactory
{
    public IDownloadClient GetDownloader(HttpRequestMessage msg, AbsolutePath outputPath, IJob job);
}

public class DownloadClientFactory(MainSettings _settings, ILoggerFactory _loggerFactory, IHttpClientFactory _httpClientFactory) : IDownloadClientFactory
{
    private readonly ILogger<NonResumableDownloadClient> _nonResuableDownloaderLogger = _loggerFactory.CreateLogger<NonResumableDownloadClient>();
    private readonly ILogger<ResumableDownloadClient> _resumableDownloaderLogger = _loggerFactory.CreateLogger<ResumableDownloadClient>();

    private NonResumableDownloadClient? _nonReusableDownloader = default;

    public IDownloadClient GetDownloader(HttpRequestMessage msg, AbsolutePath outputPath, IJob job)
    {
        if (job.Size >= _settings.MinimumFileSizeForResumableDownloadMB * 1024 * 1024)
        {
            return new ResumableDownloadClient(msg, outputPath, job, _settings.MaximumMemoryPerDownloadThreadInMB, _resumableDownloaderLogger);
        }
        else
        {
            _nonReusableDownloader ??= new NonResumableDownloadClient(msg, outputPath, _nonResuableDownloaderLogger, _httpClientFactory);

            return new NonResumableDownloadClient(msg, outputPath, _nonResuableDownloaderLogger, _httpClientFactory);
        }
    }
}
