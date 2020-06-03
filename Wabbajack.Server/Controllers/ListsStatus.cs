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
using Wabbajack.Lib.ModListRegistry;
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
            return _validator.Summaries
                .Select(d => d.Detailed)
                .FirstOrDefault(d => d.MachineName == Name);
        }

        [JsonName("Badge")]
        public class Badge
        {
            public int schemaVersion { get; set; } = 1;
            public string label { get; set; }
            public string message { get; set; }
            public string color { get; set; }

            public Badge(string _label, string _message)
            {
                label = _label;
                message = _message;
            }
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

            return Ok(new Badge("Modlist Availability", $"{ration}%"){color = color}.ToJson());
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

            return Ok(new Badge(info.Name, failing ? "Failing" : "Succeeding"){color = failing ? "red" : "green"}.ToJson());
        }
    }
}
