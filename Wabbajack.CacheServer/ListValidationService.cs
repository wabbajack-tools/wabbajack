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
using Nancy;
using Nancy.Responses;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.CacheServer
{
    public class ListValidationService : NancyModule
    {
        public class ModListStatus
        {
            public string Name;
            public DateTime Checked = DateTime.Now;
            public List<(Archive archive, bool)> Archives { get; set; }
            public DownloadMetadata DownloadMetaData { get; set; }
            public bool HasFailures { get; set; }
        }

        public static Dictionary<string, ModListStatus> ModLists { get; set; }

        public ListValidationService() : base("/lists")
        {
            Get("/status", HandleGetLists);
            Get("/status/{Name}.json", HandleGetListJson);
            Get("/status/{Name}.html", HandleGetListHtml);
        }

        private object HandleGetLists(object arg)
        {
            var summaries = ModLists.Values.Select(m => new ModlistSummary
            {
                Name = m.Name,
                Checked = m.Checked,
                Failed = m.Archives.Count(a => a.Item2),
                Passed = m.Archives.Count(a => !a.Item2),
            }).ToList();
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
        private object HandleGetListJson(dynamic arg)
        {
            var lst = ModLists[(string)arg.Name];
            var summary = new DetailedSummary
            {
                Name = lst.Name,
                Checked = lst.Checked,
                Failed = lst.Archives.Where(a => a.Item2)
                    .Select(a => new ArchiveSummary {Name = a.archive.Name, State = a.archive.State}).ToList(),
                Passed = lst.Archives.Where(a => !a.Item2)
                    .Select(a => new ArchiveSummary { Name = a.archive.Name, State = a.archive.State }).ToList(),
            };
            return summary.ToJSON();
        }

        private object HandleGetListHtml(dynamic arg)
        {
            var lst = ModLists[(string)arg.Name];
            var sb = new StringBuilder();

            sb.Append("<html><body>");
            sb.Append($"<h2>{lst.Name} - {lst.Checked}</h2>");

            var failed_list = lst.Archives.Where(a => a.Item2).ToList();
            sb.Append($"<h3>Failed ({failed_list.Count}):</h3>");
            sb.Append("<ul>");
            foreach (var archive in failed_list)
            {
                sb.Append($"<li>{archive.archive.Name}</li>");
            }
            sb.Append("</ul>");

            var pased_list = lst.Archives.Where(a => !a.Item2).ToList();
            sb.Append($"<h3>Passed ({pased_list.Count}):</h3>");
            sb.Append("<ul>");
            foreach (var archive in pased_list.OrderBy(f => f.archive.Name))
            {
                sb.Append($"<li>{archive.archive.Name}</li>");
            }
            sb.Append("</ul>");

            sb.Append("</body></html>");
            var response = (Response)sb.ToString();
            response.ContentType = "text/html";
            return response;
        }

        public static void Start()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        ValidateLists().Wait();
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }

                    // Sleep for two hours
                    Thread.Sleep(1000 * 60 * 60 * 2);
                }
            }).Start();
        }
        public static async Task ValidateLists()
        {
            Utils.Log("Cleaning Nexus Cache");
            var client = new HttpClient();
            await client.GetAsync("http://build.wabbajack.org/nexus_api_cache/update");

            Utils.Log("Starting Modlist Validation");
            var modlists = await ModlistMetadata.LoadFromGithub();

            var statuses = new Dictionary<string, ModListStatus>();

            using (var queue = new WorkQueue())
            {
                foreach (var list in modlists)
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

                                return (archive, is_failed);
                            }))
                        .ToList();


                    var status = new ModListStatus
                    {
                        Name = list.Title, 
                        Archives = validated.OrderBy(v => v.archive.Name).ToList(),
                        DownloadMetaData = list.DownloadMetadata,
                        HasFailures = validated.Any(v => v.is_failed)
                    };

                    statuses.Add(status.Name, status);
                 }
            }

            Utils.Log($"Done validating {statuses.Count} lists");
            ModLists = statuses;
        }
    }
}

