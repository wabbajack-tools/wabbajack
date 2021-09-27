using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Http
{
    public class SingleThreadedDownloader : IHttpDownloader
    {
        private readonly ILogger<SingleThreadedDownloader> _logger;
        private readonly HttpClient _client;

        public SingleThreadedDownloader(ILogger<SingleThreadedDownloader> logger, HttpClient client)
        {
            _logger = logger;
            _client = client;
        }
        public async Task<Hash> Download(HttpRequestMessage message, AbsolutePath outputPath, IJob job, CancellationToken token)
        {
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
                throw new HttpException(response);
            
            if (job.Size == 0) 
                job.Size = response.Content.Headers.ContentLength ?? 0;
            
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var outputStream = outputPath.Open(FileMode.Create, FileAccess.Write);
            return await stream.HashingCopy(outputStream, token, job);
        }
    }
}