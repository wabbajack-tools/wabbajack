using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CouchDB.Driver.Extensions;
using Nancy;
using Nancy.Responses;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.CacheServer
{
    public class ListValidationService : NancyModule
    {
        public ListValidationService() : base("/lists")
        {
            Get("/status", HandleGetLists);
            Get("/status/{Name}.json", HandleGetListJson);
            Get("/status/{Name}.html", HandleGetListHtml);
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

        private async Task<Response> HandleGetListHtml(dynamic arg)
        {
            var lst = (await ModListStatus.ByName((string)arg.Name)).DetailedStatus;
            var sb = new StringBuilder();

            sb.Append("<html><body>");
            sb.Append($"<h2>{lst.Name} - {lst.Checked}</h2>");

            var failed_list = lst.Archives.Where(a => a.IsFailing).ToList();
            sb.Append($"<h3>Failed ({failed_list.Count}):</h3>");
            sb.Append("<ul>");
            foreach (var archive in failed_list)
            {
                sb.Append($"<li>{archive.Archive.Name}</li>");
            }
            sb.Append("</ul>");

            var pased_list = lst.Archives.Where(a => !a.IsFailing).ToList();
            sb.Append($"<h3>Passed ({pased_list.Count}):</h3>");
            sb.Append("<ul>");
            foreach (var archive in pased_list.OrderBy(f => f.Archive.Name))
            {
                sb.Append($"<li>{archive.Archive.Name}</li>");
            }
            sb.Append("</ul>");

            sb.Append("</body></html>");
            var response = (Response)sb.ToString();
            response.ContentType = "text/html";
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
        public static async Task ValidateLists()
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
                        await ValidateList(list, queue);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }

            Utils.Log($"Done validating {modlists.Count} lists");
        }

        private static async Task ValidateList(ModlistMetadata list, WorkQueue queue)
        {
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

