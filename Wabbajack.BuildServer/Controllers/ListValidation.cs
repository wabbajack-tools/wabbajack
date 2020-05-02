using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListValidation : AControllerBase<ListValidation>
    {
        public enum ArchiveStatus
        {
            Valid,
            InValid,
            Updating,
            Updated,
        }
        
        public ListValidation(ILogger<ListValidation> logger, SqlService sql, IMemoryCache cache, AppSettings settings) : base(logger, sql)
        {
            _updater = new ModlistUpdater(null, sql, settings);
            _settings = settings;
            Cache = cache;
            _nexusClient = NexusApiClient.Get();

        }

        public static IMemoryCache Cache { get; set; }
        public const string ModListSummariesKey = "ModListSummaries";

        public static void ResetCache()
        {
            SummariesLastChecked = DateTime.UnixEpoch;
            ModListSummaries = null;
        }

        private static IEnumerable<(ModListSummary Summary, DetailedStatus Detailed)> ModListSummaries = null;
        public static DateTime SummariesLastChecked = DateTime.UnixEpoch;
        private static AsyncLock UpdateLock = new AsyncLock();
        public async Task<IEnumerable<(ModListSummary Summary, DetailedStatus Detailed)>> GetSummaries()
        {
            static bool TimesUp()
            {
                return DateTime.UtcNow - SummariesLastChecked > TimeSpan.FromMinutes(5);
            }
            
            if (ModListSummaries != null && !TimesUp())
            {
                return ModListSummaries;
            }

            var task = Task.Run(async () =>
            {
                using var _ = await UpdateLock.WaitAsync();
                if (ModListSummaries != null && !TimesUp())
                {
                    return ModListSummaries;
                }
                SummariesLastChecked = DateTime.UtcNow;

                
                var data = await SQL.GetValidationData();

                using var queue = new WorkQueue();

                var results = await data.ModLists.PMap(queue, async list =>
                {
                    var (metadata, modList) = list;
                    var archives = await modList.Archives.PMap(queue, async archive =>
                    {
                        var (_, result) = await ValidateArchive(data, archive);
                        if (result != ArchiveStatus.InValid) return (archive, result);

                        return await TryToFix(data, archive);

                    });

                    var failedCount = archives.Count(f => f.Item2 == ArchiveStatus.InValid);
                    var passCount = archives.Count(f =>
                        f.Item2 == ArchiveStatus.Valid || f.Item2 == ArchiveStatus.Updated);
                    var updatingCount = archives.Count(f => f.Item2 == ArchiveStatus.Updating);

                    var summary = new ModListSummary
                    {
                        Checked = DateTime.UtcNow,
                        Failed = failedCount,
                        Passed = passCount,
                        Updating = updatingCount,
                        MachineURL = metadata.Links.MachineURL,
                        Name = metadata.Title,
                    };

                    var detailed = new DetailedStatus
                    {
                        Name = metadata.Title,
                        Checked = DateTime.UtcNow,
                        DownloadMetaData = metadata.DownloadMetadata,
                        HasFailures = failedCount > 0,
                        MachineName = metadata.Links.MachineURL,
                        Archives = archives.Select(a =>
                        {
                            a.Item1.Meta = "";
                            return new DetailedStatusItem
                            {
                                Archive = a.Item1,
                                IsFailing = a.Item2 == ArchiveStatus.InValid || a.Item2 == ArchiveStatus.Updating
                            };
                        }).ToList()
                    };

                    return (summary, detailed);
                });


                var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                Cache.Set(ModListSummariesKey, results, cacheOptions);
                
                ModListSummaries = results;
                return results;
            });
            var data = ModListSummaries;
            if (data == null)
                return await task;
            return data;
        }

        private async Task<(Archive archive, ArchiveStatus)> ValidateArchive(SqlService.ValidationData data, Archive archive)
        {
            switch (archive.State)
            {
                case GoogleDriveDownloader.State _:
                    // Disabled for now due to GDrive rate-limiting the build server
                    return (archive, ArchiveStatus.Valid);
                case NexusDownloader.State nexusState when data.NexusFiles.Contains((
                    nexusState.Game.MetaData().NexusGameId, nexusState.ModID, nexusState.FileID)):
                    return (archive, ArchiveStatus.Valid);
                case NexusDownloader.State ns:
                    return (archive, await FastNexusModStats(ns));
                case HTTPDownloader.State s when new Uri(s.Url).Host.StartsWith("wabbajackpush"):
                    return (archive, ArchiveStatus.Valid);
                case ManualDownloader.State _:
                    return (archive, ArchiveStatus.Valid);
                default:
                {
                    if (data.ArchiveStatus.TryGetValue((archive.State.PrimaryKeyString, archive.Hash),
                        out bool isValid))
                    {
                        return isValid ? (archive, ArchiveStatus.Valid) : (archive, ArchiveStatus.InValid);
                    }

                    return (archive, ArchiveStatus.InValid);
                }
            }
        }

        private async Task<ArchiveStatus> FastNexusModStats(NexusDownloader.State ns)
        {
            
            var mod = await SQL.GetNexusModInfoString(ns.Game, ns.ModID);
            var files = await SQL.GetModFiles(ns.Game, ns.ModID);

            try
            {
                if (mod == null)
                {
                    Utils.Log($"Found missing Nexus mod info {ns.Game} {ns.ModID}");
                    try
                    {
                        mod = await (await _nexusClient).GetModInfo(ns.Game, ns.ModID, false);
                    }
                    catch
                    {
                        mod = new ModInfo
                        {
                            mod_id = ns.ModID.ToString(), game_id = ns.Game.MetaData().NexusGameId, available = false
                        };
                    }

                    try
                    {
                        await SQL.AddNexusModInfo(ns.Game, ns.ModID, mod.updated_time, mod);
                    }
                    catch (Exception _)
                    {
                        // Could be a PK constraint failure
                    }

                }

                if (files == null)
                {
                    Utils.Log($"Found missing Nexus mod file infos {ns.Game} {ns.ModID}");
                    try
                    {
                        files = await (await _nexusClient).GetModFiles(ns.Game, ns.ModID, false);
                    }
                    catch
                    {
                        files = new NexusApiClient.GetModFilesResponse {files = new List<NexusFileInfo>()};
                    }

                    try
                    {
                        await SQL.AddNexusModFiles(ns.Game, ns.ModID, mod.updated_time, files);
                    }
                    catch (Exception _)
                    {
                        // Could be a PK constraint failure
                    }
                }
            }
            catch (Exception ex)
            {
                return ArchiveStatus.InValid;
            }

            if (mod.available && files.files.Any(f => !string.IsNullOrEmpty(f.category_name) && f.file_id == ns.FileID))
                return ArchiveStatus.Valid;
            return ArchiveStatus.InValid;

        }

        private static AsyncLock _findPatchLock = new AsyncLock();
        private async Task<(Archive, ArchiveStatus)> TryToFix(SqlService.ValidationData data, Archive archive)
        {
            using var _ = await _findPatchLock.WaitAsync();

            var result = await _updater.GetAlternative(archive.Hash.ToHex());
            return result switch
            {
                OkObjectResult ok => (archive, ArchiveStatus.Updated),
                OkResult ok => (archive, ArchiveStatus.Updated),
                AcceptedResult accept => (archive, ArchiveStatus.Updating),
                _ => (archive, ArchiveStatus.InValid)
            };
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

        private AppSettings _settings;
        private ModlistUpdater _updater;
        private Task<NexusApiClient> _nexusClient;

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
