using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListsStatus : ControllerBase
    {
        private ILogger<ListsStatus> _logger;
        private ListValidator _validator;

        public ListsStatus(ILogger<ListsStatus> logger, ListValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }
        
        [HttpGet]
        [Route("status.json")]
        public async Task<IEnumerable<ModListSummary>> HandleGetLists()
        {
            return (_validator.Summaries).Select(d => d.Summary);
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
            return Ok((await DetailedStatus(Name)).ToJson());
        }
        
        private async Task<DetailedStatus> DetailedStatus(string Name)
        {
            var results = _validator.Summaries
                .Select(d => d.Detailed)
                .FirstOrDefault(d => d.MachineName == Name);
            results!.Archives.Do(itm =>
            {
                if (string.IsNullOrWhiteSpace(itm.Archive.Name)) 
                    itm.Archive.Name = itm.Archive.State.PrimaryKeyString;
            });
            results.Archives = results.Archives.OrderBy(a => a.Name).ToList();
            return results;
        }

        [HttpGet]
        [Route("status/badge.json")]
        public async Task<IActionResult> HandleGitHubBadge()
        {
            //var failing = _validator.Summaries.Select(x => x.Summary.Failed).Aggregate((x, y) => x + y);
            var succeeding = _validator.Summaries.Select(x => x.Summary.Passed).Aggregate((x, y) => x + y);
            var total = _validator.Summaries.Count();
            double ration = total / (double)succeeding * 100.0;
            string color;
            if (ration >= 95)
                color = "brightgreen";
            else if (ration >= 80)
                color = "green";
            else if (ration >= 50)
                color = "yellowgreen";
            else if (ration >= 20)
                color = "orange";
            else
                color = "red";

            Response.ContentType = "application/json";
            return Ok(new Badge("Modlist Availability", $"{ration}%"){color = color});
        }

        [HttpGet]
        [Route("status/{Name}/badge.json")]
        public async Task<IActionResult> HandleNamedGitHubBadge(string Name)
        {
            var info = _validator.Summaries.Select(x => x.Summary)
                .FirstOrDefault(x => x.MachineURL == Name);

            if (info == null)
                return new NotFoundObjectResult("Not Found!");

            var failing = info.HasFailures;

            Response.ContentType = "application/json";
            return Ok(new Badge(info.Name, failing ? "Failing" : "Succeeding"){color = failing ? "red" : "green"});
        }
    }
}
