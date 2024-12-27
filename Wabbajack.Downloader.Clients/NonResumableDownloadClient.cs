using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloader.Clients;

internal class NonResumableDownloadClient(HttpRequestMessage _msg, AbsolutePath _outputPath, ILogger<NonResumableDownloadClient> _logger, IHttpClientFactory _httpClientFactory) : IDownloadClient
{
    public async Task<Hash> Download(CancellationToken token, int retry = 3)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("SmallFilesClient");
            var response = await httpClient.GetStreamAsync(_msg.RequestUri!.ToString());
            await using var fileStream = _outputPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await response.CopyToAsync(fileStream, token);
            fileStream.Close();
            await using var file = _outputPath.Open(FileMode.Open);
            return await file.Hash(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download '{name}' after 3 tries.", _outputPath.FileName.ToString());

            if (retry <= 3)
            {
                return await Download(token, retry--);
            }

            return new Hash();
        }
    }
}