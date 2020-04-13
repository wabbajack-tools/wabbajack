using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListValidation : AControllerBase<ListValidation>
    {
        public ListValidation(ILogger<ListValidation> logger, SqlService sql) : base(logger, sql)
        {
        }

        public async Task<IEnumerable<(ModListSummary Summary, DetailedStatus Detailed)>> GetSummaries()
        {
            var data = await SQL.GetValidationData();
            
            using var queue = new WorkQueue();

            var results = data.ModLists.PMap(queue, list =>
            {
                var archives = list.ModList.Archives.Select(archive =>
                {
                    switch (archive.State)
                    {
                        case NexusDownloader.State nexusState when data.NexusFiles.Contains((
                            nexusState.Game.MetaData().NexusGameId, nexusState.ModID, nexusState.FileID)):
                            return (archive, true);
                        case NexusDownloader.State nexusState:
                            return (archive, false);
                        case ManualDownloader.State _:
                            return (archive, true);
                        default:
                        {
                            if (data.ArchiveStatus.TryGetValue((archive.State.PrimaryKeyString, archive.Hash),
                                out var isValid))
                            {
                                return (archive, isValid);
                            }

                            return (archive, false);
                        }
                    }
                }).ToList();

                var failedCount = archives.Count(f => !f.Item2);
                var passCount = archives.Count(f => f.Item2);

                var summary =  new ModListSummary
                {
                    Checked = DateTime.UtcNow,
                    Failed = failedCount,
                    MachineURL = list.Metadata.Links.MachineURL,
                    Name = list.Metadata.Title,
                    Passed = passCount
                };

                var detailed = new DetailedStatus
                {
                    Name = list.Metadata.Title,
                    Checked = DateTime.UtcNow,
                    DownloadMetaData = list.Metadata.DownloadMetadata,
                    HasFailures = failedCount > 0,
                    MachineName = list.Metadata.Links.MachineURL,
                    Archives = archives.Select(a => new DetailedStatusItem
                    {
                        Archive = a.archive, IsFailing = !a.Item2
                    }).ToList()
                };

                return (summary, detailed);
            });

            return await results;
        }
        
        [HttpGet]
        [Route("status.json")]
        public async Task<IEnumerable<ModListSummary>> HandleGetLists()
        {
            return (await GetSummaries()).Select(d => d.Summary);
        }

        private static readonly Func<object, string> HandleGetRssFeedTemplate = NettleEngine.GetCompiler().Compile(@"
<?xml version=""1.0""?>
<rss version=""2.0"">
  <channel>
    <title>{{lst.Name}} - Broken Mods</title>
    <link>http://build.wabbajack.org/status/{{lst.Name}}.html</link>
    <description>These are mods that are broken and need updating</description>
    {{ each $.failed }}
    <item>
       <title>{{$.Archive.Name}} {{$.Archive.Hash}} {{$.Archive.State.PrimaryKeyString}}</title>
       <link>{{$.Archive.Name}}</link>
    </item>
    {{/each}}
  </channel>
</rss>
        ");

        [HttpGet]
        [Route("status/{Name}/broken.rss")]
        public async Task<ContentResult> HandleGetRSSFeed(string Name)
        {
            var lst = await DetailedStatus(Name);
            var response = HandleGetRssFeedTemplate(new
            {
                lst,
                failed = lst.Archives.Where(a => a.IsFailing).ToList(),
                passed = lst.Archives.Where(a => !a.IsFailing).ToList()
            });
            return new ContentResult
            {
                ContentType = "application/rss+xml",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
        }
        
        private static readonly Func<object, string> HandleGetListTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>{{lst.Name}} - {{lst.Checked}} - {{ago}}min ago</h2>
                <h3>Failed ({{failed.Count}}):</h3>
                <ul>
                {{each $.failed }}
                <li>{{$.Archive.Name}}</li>
                {{/each}}
                </ul>
                <h3>Passed ({{passed.Count}}):</h3>
                <ul>
                {{each $.passed }}
                <li>{{$.Archive.Name}}</li>
                {{/each}}
                </ul>
            </body></html>
        ");

        [HttpGet]
        [Route("status/{Name}.html")]
        public async Task<ContentResult> HandleGetListHtml(string Name)
        {

            var lst = await DetailedStatus(Name);
            var response = HandleGetListTemplate(new
            {
                lst,
                ago = (DateTime.UtcNow - lst.Checked).TotalMinutes,
                failed = lst.Archives.Where(a => a.IsFailing).ToList(),
                passed = lst.Archives.Where(a => !a.IsFailing).ToList()
            });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
        }
        
        [HttpGet]
        [Route("status/{Name}.json")]
        public async Task<IActionResult> HandleGetListJson(string Name)
        {
            return Ok((await DetailedStatus(Name)).ToJson());
        }

        private async Task<DetailedStatus> DetailedStatus(string Name)
        {
            return (await GetSummaries())
                .Select(d => d.Detailed)
                .FirstOrDefault(d => d.MachineName == Name);
        }
    }
}
