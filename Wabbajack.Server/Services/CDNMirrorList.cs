using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;

namespace Wabbajack.Server.Services
{
    public class CDNMirrorList : AbstractService<ListValidator, int>
    {
        public CDNMirrorList(ILogger<ListValidator> logger, AppSettings settings, QuickSync quickSync) : base(logger, settings, quickSync, TimeSpan.FromHours(1))
        {
        }
        public string[] Mirrors { get; private set; }
        public DateTime LastUpdate { get; private set; }

        public override async Task<int> Execute()
        {
            var client = new Lib.Http.Client();
            var json =  await client.GetStringAsync("https://bunnycdn.com/api/system/edgeserverlist");
            client.Headers.Add(("Host", "wabbajack.b-cdn.net"));
            using var queue = new WorkQueue();
            var mirrors = json.FromJsonString<string[]>();
            _logger.LogInformation($"Found {mirrors.Length} edge severs");

            var servers = (await mirrors
                .PMap(queue, async ip =>
                {
                    try
                    {
                        // We use a volume server, so this file will only exist on some (lower cost) servers
                        using var result = await client.GetAsync(
                            $"https://{ip}/WABBAJACK_TEST_FILE.zip_48f799f6-39b2-4229-a329-7459c9965c2d/definition.json.gz",
                            errorsAsExceptions: false, retry: false);
                        var data = await result.Content.ReadAsByteArrayAsync();
                        return (ip, use: result.IsSuccessStatusCode, size : data.Length);
                    }
                    catch (Exception)
                    {
                        return (ip, use : false, size: 0);
                    }
                }))
                .Where(r => r.use && r.size == 267)
                .Select(r => r.ip)
                .ToArray();
            _logger.LogInformation($"Found {servers.Length} valid mirrors");
            Mirrors = servers;
            LastUpdate = DateTime.UtcNow;
            return Mirrors.Length;

        }
    }
}
