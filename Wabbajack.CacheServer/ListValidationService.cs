using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MongoDB.Driver;
using Nancy;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using MongoDB.Driver.Linq;
using Nettle;
using Nettle.Functions;

namespace Wabbajack.CacheServer
{
    public class ListValidationService : NancyModule
    {
        public ListValidationService() : base("/lists")
        {
            Get("/status", HandleGetLists);
            Get("/force_recheck", HandleForceRecheck);
            Get("/status/{Name}.json", HandleGetListJson);
            Get("/status/{Name}.html", HandleGetListHtml);
            Get("/status/{Name}/broken.rss", HandleGetRSSFeed);

        }

        private async Task<string> HandleForceRecheck(object arg)
        {
            await ValidateLists(false);
            return "done";
        }

        private async Task<string> HandleGetLists(object arg)
        {
            var summaries = await ModListStatus.All.Select(m => m.Summary).ToListAsync();
            return summaries.ToJSON();
        }

        public class ArchiveSummary
        {
            public string Name;
            public AbstractDownloadState State;
        }
        public class DetailedSummary
        {
            public string Name;
            public DateTime Checked;
            public List<ArchiveSummary> Failed;
            public List<ArchiveSummary> Passed;

        }
        private async Task<string> HandleGetListJson(dynamic arg)
        {
            var metric = Metrics.Log("list_validation.get_list_json", (string)arg.Name);
            var lst = (await ModListStatus.ByName((string)arg.Name)).DetailedStatus;
            return lst.ToJSON();
        }

        
        private static readonly Func<object, string> HandleGetListTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>{{lst.Name}} - {{lst.Checked}}</h2>
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

        private async Task<Response> HandleGetListHtml(dynamic arg)
        {

            var lst = (await ModListStatus.ByName((string)arg.Name)).DetailedStatus;
            var response = (Response)HandleGetListTemplate(new
            {
                lst,
                failed = lst.Archives.Where(a => a.IsFailing).ToList(),
                passed = lst.Archives.Where(a => !a.IsFailing).ToList()
            });
            response.ContentType = "text/html";
            return response;
        }

        private static readonly Func<object, string> HandleGetRSSFeedTemplate = NettleEngine.GetCompiler().Compile(@"
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

        public async Task<Response> HandleGetRSSFeed(dynamic arg)
        {
            var metric = Metrics.Log("failed_rss", arg.Name);
            var lst = (await ModListStatus.ByName((string)arg.Name)).DetailedStatus;
            var response = (Response)HandleGetRSSFeedTemplate(new
            {
                lst,
                failed = lst.Archives.Where(a => a.IsFailing).ToList(),
                passed = lst.Archives.Where(a => !a.IsFailing).ToList()
            });
            response.ContentType = "application/rss+xml";
            await metric;
            return response;

        }

        public static void Start()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await ValidateLists();
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }

                    // Sleep for two hours
                    await Task.Delay(1000 * 60 * 60 * 2);
                }
            }).FireAndForget();
        }
        public static async Task ValidateLists(bool skipIfNewer = true)
        {
            Utils.Log("Cleaning Nexus Cache");
            var client = new HttpClient();
            //await client.GetAsync("http://build.wabbajack.org/nexus_api_cache/update");

            Utils.Log("Starting Modlist Validation");
            var modlists = await ModlistMetadata.LoadFromGithub();

            using (var queue = new WorkQueue())
            {
                foreach (var list in modlists)
                {
                    try
                    {
                        await ValidateList(list, queue, skipIfNewer);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }
                }
            }

            Utils.Log($"Done validating {modlists.Count} lists");
        }

        private static async Task ValidateList(ModlistMetadata list, WorkQueue queue, bool skipIfNewer = true)
        {
            var existing = await Server.Config.ListValidation.Connect().FindOneAsync(l => l.Id == list.Links.MachineURL);
            if (skipIfNewer && existing != null && DateTime.Now - existing.DetailedStatus.Checked < TimeSpan.FromHours(2))
                return;

            var modlist_path = Path.Combine(Consts.ModListDownloadFolder, list.Links.MachineURL + ExtensionManager.Extension);

            if (list.NeedsDownload(modlist_path))
            {
                if (File.Exists(modlist_path))
                    File.Delete(modlist_path);

                var state = DownloadDispatcher.ResolveArchive(list.Links.Download);
                Utils.Log($"Downloading {list.Links.MachineURL} - {list.Title}");
                await state.Download(modlist_path);
            }
            else
            {
                Utils.Log($"No changes detected from downloaded modlist");
            }


            Utils.Log($"Loading {modlist_path}");

            var installer = AInstaller.LoadFromFile(modlist_path);

            Utils.Log($"{installer.Archives.Count} archives to validate");

            DownloadDispatcher.PrepareAll(installer.Archives.Select(a => a.State));

            var validated = (await installer.Archives
                    .PMap(queue, async archive =>
                    {
                        Utils.Log($"Validating: {archive.Name}");
                        bool is_failed;
                        try
                        {
                            is_failed = !(await archive.State.Verify());
                        }
                        catch (Exception)
                        {
                            is_failed = false;
                        }

                        return new DetailedStatusItem {IsFailing = is_failed, Archive = archive};
                    }))
                .ToList();


            var status = new DetailedStatus
            {
                Name = list.Title,
                Archives = validated.OrderBy(v => v.Archive.Name).ToList(),
                DownloadMetaData = list.DownloadMetadata,
                HasFailures = validated.Any(v => v.IsFailing)
            };

            var dto = new ModListStatus
            {
                Id = list.Links.MachineURL,
                Summary = new ModlistSummary
                {
                    Name = status.Name,
                    Checked = status.Checked,
                    Failed = status.Archives.Count(a => a.IsFailing),
                    Passed = status.Archives.Count(a => !a.IsFailing),
                },
                DetailedStatus = status,
                Metadata = list
            };
            await ModListStatus.Update(dto);
        }
    }
}

