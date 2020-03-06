using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.Models.Jobs
{
    /// <summary>
    /// DynDOLOD is really hosted on a STEP Forum post as a series of MEGA links. The Nexus URLs come and go
    /// but the real releases are on STEP. So let's keep that data fresh.
    /// </summary>
    public class IndexDynDOLOD : AJobPayload
    {
        public override string Description => "Queue MEGA URLs from the DynDOLOD Post";
        public override async Task<JobResult> Execute(DBContext db, SqlService sql, AppSettings settings)
        {
            var doc = new HtmlDocument();
            var body = await new Common.Http.Client().GetStringAsync("https://forum.step-project.com/topic/13894-dyndolod-beta-for-skyrim-special-edition-and-skyrim-vr-279/");
            doc.LoadHtml(body);

            var matches =
                doc.DocumentNode
                    .Descendants()
                    .Where(d=> d.NodeType == HtmlNodeType.Element && d.Attributes.Contains("href"))
                    .Select(d => d.Attributes["href"].Value)
                .Select(m => Uri.TryCreate(m.ToString(), UriKind.Absolute, out var result) ? result : null)
                .Where(uri => uri != null && uri.Host == "mega.nz")
                .Select(url => new Job()
                {
                    Payload = new IndexJob
                    {
                        Archive = new Archive
                        {
                            Name = Guid.NewGuid() + ".7z",
                            State = new MegaDownloader.State
                            {
                                Url = url.ToString()
                            }
                        }
                    }
                })
                .ToList();


            foreach (var job in matches)
            {
                var key = ((MegaDownloader.State)((IndexJob)job.Payload).Archive.State).PrimaryKeyString;
                var found = await db.DownloadStates.AsQueryable().Where(s => s.Key == key).FirstOrDefaultAsync();
                if (found != null) continue;

                Utils.Log($"Queuing {key} for indexing");
                await db.Jobs.InsertOneAsync(job);
            }

            return JobResult.Success();

        }
    }
}
