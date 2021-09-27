using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Server;
using Wabbajack.Server.Services;
namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListsStatus : ControllerBase
    {
        private ILogger<ListsStatus> _logger;

        public ListsStatus(ILogger<ListsStatus> logger)
        {
            _logger = logger;
        }
        
        [HttpGet]
        [Route("status.json")]
        public async Task<IEnumerable<ModListSummary>> HandleGetLists()
        {
            throw new NotImplementedException();
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
                {{if $.HasUrl}}
                <li><a href='{{$.Url}}'>{{$.Name}}</a></li>
                {{else}}
                <li>{{$.Name}}</li>
                {{/if}}
                {{/each}}
                </ul>


                <h3>Updated ({{updated.Count}}):</h3>
                <ul>
                {{each $.updated }}
                {{if $.HasUrl}}
                <li><a href='{{$.Url}}'>{{$.Name}}</a></li>
                {{else}}
                <li>{{$.Name}}</li>
                {{/if}}

                {{/each}}
                </ul>

                <h3>Mirrored ({{mirrored.Count}}):</h3>
                <ul>
                {{each $.mirrored }}
                {{if $.HasUrl}}
                <li><a href='{{$.Url}}'>{{$.Name}}</a></li>
                {{else}}
                <li>{{$.Name}}</li>
                {{/if}}

                {{/each}}
                </ul>

                <h3>Updating ({{updating.Count}}):</h3>
                <ul>
                {{each $.updating }}
                {{if $.HasUrl}}
                <li><a href='{{$.Url}}'>{{$.Name}}</a></li>
                {{else}}
                <li>{{$.Name}}</li>
                {{/if}}
                {{/each}}
                </ul>

                <h3>Passed ({{passed.Count}}):</h3>
                <ul>
                {{each $.passed }}
                {{if $.HasUrl}}
                <li><a href='{{$.Url}}'>{{$.Name}}</a></li>
                {{else}}
                <li>{{$.Name}}</li>
                {{/if}}
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
                passed = lst.Archives.Where(a => !a.IsFailing).ToList(),
                updated = lst.Archives.Where(a => a.ArchiveStatus == ArchiveStatus.Updated).ToList(),
                updating = lst.Archives.Where(a => a.ArchiveStatus == ArchiveStatus.Updating).ToList(),
                mirrored = lst.Archives.Where(a => a.ArchiveStatus == ArchiveStatus.Mirrored).ToList()
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
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IActionResult> HandleGetListJson(string Name)
        {
            var lst = await DetailedStatus(Name);
            if (lst == default) return NotFound();
            return Ok(lst);
        }
        
        private async Task<DetailedStatus?> DetailedStatus(string Name)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Route("status/badge.json")]
        public async Task<IActionResult> HandleGitHubBadge()
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Route("status/{Name}/badge.json")]
        public async Task<IActionResult> HandleNamedGitHubBadge(string Name)
        {
            throw new NotImplementedException();
        }
    }
}
