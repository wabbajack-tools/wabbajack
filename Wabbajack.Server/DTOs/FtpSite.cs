using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;

namespace Wabbajack.Server.DTOs
{
    public enum StorageSpace
    {
        AuthoredFiles,
        Patches,
        Mirrors
    }
    
    public class FtpSite
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Hostname { get; set; }

        public async Task<FtpClient> GetClient(ILogger logger)
        {
            return await CircuitBreaker.WithAutoRetryAllAsync(logger, async () =>
            {
                var ftpClient = new FtpClient(Hostname, new NetworkCredential(Username, Password));
                await ftpClient.ConnectAsync();
                return ftpClient;
            });
        }
    }
}
