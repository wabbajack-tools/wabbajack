using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.Models.Jobs
{
    /// <summary>
    /// DynDOLOD is really hosted on a STEP Forum post as a series of MEGA links. The Nexus URLs come and go
    /// but the real releases are on STEP. So let's keep that data fresh.
    /// </summary>
    [JsonName("IndexDynDOLOD")]
    public class IndexDynDOLOD : AJobPayload
    {
        public override string Description => "Queue MEGA URLs from the DynDOLOD Post";
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            var doc = new HtmlDocument();
            var body = await new HttpClient().GetStringAsync(new Uri(
                "https://forum.step-project.com/topic/13894-dyndolod-beta-for-skyrim-special-edition-and-skyrim-vr-279/"));
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
                        Archive = new Archive(new MegaDownloader.State(url.ToString()))
                        {
                            Name = Guid.NewGuid() + ".7z",
                        }
                    }
                })
                .ToList();


            foreach (var job in matches)
            {
                var key = ((MegaDownloader.State)((IndexJob)job.Payload).Archive.State).PrimaryKeyString;
                var found = await sql.DownloadStateByPrimaryKey(key);
                if (found != null) continue;

                Utils.Log($"Queuing {key} for indexing");
                await sql.EnqueueJob(job);
            }

            return JobResult.Success();

        }

        protected override IEnumerable<object> PrimaryKey => new object[0];
    }
}
