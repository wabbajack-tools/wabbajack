using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloader.Services;

internal class NonResumableDownloadClient(HttpRequestMessage _msg, AbsolutePath _outputPath, ILogger<NonResumableDownloadClient> _logger, IHttpClientFactory _httpClientFactory) : IDownloadClient
{
    public async Task<Hash> Download(CancellationToken token)
    {
        Stream? fileStream;

        try
        {
            fileStream = _outputPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open file path '{filePath}'. Throwing...", _outputPath.FileName.ToString());

            throw;
        }

        try
        {
            _logger.LogDebug("Download for '{name}' is starting from scratch...", _outputPath.FileName.ToString());

            var httpClient = _httpClientFactory.CreateClient("SmallFilesClient");
            var response = await httpClient.GetStreamAsync(_msg.RequestUri!.ToString());
            await response.CopyToAsync(fileStream, token);
            fileStream.Close();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download for '{name}' encountered error. Throwing...", _outputPath.FileName.ToString());

            throw;
        }

        try
        {
            await using var file = _outputPath.Open(FileMode.Open);
            return await file.Hash(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not hash file '{filePath}'. Throwing...", _outputPath.FileName.ToString());

            throw;
        }
    }
}