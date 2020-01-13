using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nettle;
using Wabbajack.BuildServer.Models;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListValidation : AControllerBase<ListValidation>
    {
        public ListValidation(ILogger<ListValidation> logger, DBContext db) : base(logger, db)
        {
        }
        
        [HttpGet]
        [Route("status.json")]
        public async Task<IList<ModlistSummary>> HandleGetLists()
        {
            return await Db.ModListStatus.AsQueryable().Select(m => m.Summary).ToListAsync();
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
       <title>{{$.Archive.Name}}</title>
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
            var lst = (await ModListStatus.ByName(Db, Name)).DetailedStatus;
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

            var lst = (await ModListStatus.ByName(Db, Name)).DetailedStatus;
            var response = HandleGetListTemplate(new
            {
                lst,
                ago = (DateTime.Now - lst.Checked).TotalMinutes,
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


    }
}
