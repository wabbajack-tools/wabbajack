using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Mvc;
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

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]
    public class ListValidation : AControllerBase<ListValidation>
    {
        enum ArchiveStatus
        {
            Valid,
            InValid,
            Updating,
            Updated,
        }
        
        public ListValidation(ILogger<ListValidation> logger, SqlService sql, AppSettings settings) : base(logger, sql)
        {
            _settings = settings;
        }

        public async Task<IEnumerable<(ModListSummary Summary, DetailedStatus Detailed)>> GetSummaries()
        {
            var data = await SQL.GetValidationData();
            
            using var queue = new WorkQueue();

            var results = data.ModLists.PMap(queue, list =>
            {
                var (metadata, modList) = list;
                var archives = modList.Archives.Select(archive => ValidateArchive(data, archive)).ToList();

                var failedCount = archives.Count(f => f.Item2 == ArchiveStatus.InValid);
                var passCount = archives.Count(f => f.Item2 == ArchiveStatus.Valid || f.Item2 == ArchiveStatus.Updated);

                var summary =  new ModListSummary
                {
                    Checked = DateTime.UtcNow,
                    Failed = failedCount,
                    MachineURL = metadata.Links.MachineURL,
                    Name = metadata.Title,
                    Passed = passCount
                };

                var detailed = new DetailedStatus
                {
                    Name = metadata.Title,
                    Checked = DateTime.UtcNow,
                    DownloadMetaData = metadata.DownloadMetadata,
                    HasFailures = failedCount > 0,
                    MachineName = metadata.Links.MachineURL,
                    Archives = archives.Select(a => new DetailedStatusItem
                    {
                        Archive = a.archive, IsFailing = a.Item2 == ArchiveStatus.InValid || a.Item2 == ArchiveStatus.Updating
                    }).ToList()
                };

                return (summary, detailed);
            });

            return await results;
        }

        private static (Archive archive, ArchiveStatus) ValidateArchive(SqlService.ValidationData data, Archive archive)
        {
            switch (archive.State)
            {
                case NexusDownloader.State nexusState when data.NexusFiles.Contains((
                    nexusState.Game.MetaData().NexusGameId, nexusState.ModID, nexusState.FileID)):
                    return (archive, ArchiveStatus.Valid);
                case NexusDownloader.State nexusState:
                    return (archive, ArchiveStatus.InValid);
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

        private static AsyncLock _findPatchLock = new AsyncLock();
        private async Task<(Archive, ArchiveStatus)> TryToFix(SqlService.ValidationData data, Archive archive)
        {
            using var _ = await _findPatchLock.Wait();
            try
            {
                // Find all possible patches
                var patches = data.ArchivePatches
                    .Where(patch =>
                        patch.SrcHash == archive.Hash &&
                        patch.SrcState.PrimaryKeyString == archive.State.PrimaryKeyString)
                    .ToList();

                // Any that are finished
                if (patches.Where(patch => patch.DestHash != default)
                    .Where(patch =>
                        ValidateArchive(data, new Archive {State = patch.DestState, Hash = patch.DestHash}).Item2 ==
                        ArchiveStatus.Valid)
                    .Any(patch => patch.CDNPath != null))
                    return (archive, ArchiveStatus.Updated);

                // Any that are in progress
                if (patches.Any(patch => patch.CDNPath == null))
                    return (archive, ArchiveStatus.Updating);

                // Can't upgrade, don't have the original archive
                if (_settings.PathForArchive(archive.Hash) == default)
                    return (archive, ArchiveStatus.InValid);


                switch (archive.State)
                {
                    case NexusDownloader.State nexusState:
                    {
                        var otherFiles = await SQL.GetModFiles(nexusState.Game, nexusState.ModID);
                        var modInfo = await SQL.GetNexusModInfoString(nexusState.Game, nexusState.ModID);
                        if (modInfo == null || !modInfo.available || otherFiles == null || !otherFiles.files.Any())
                            return (archive, ArchiveStatus.InValid);



                        var file = otherFiles.files
                            .Where(f => f.category_name != null)
                            .OrderByDescending(f => f.uploaded_time)
                            .FirstOrDefault();

                        if (file == null) return (archive, ArchiveStatus.InValid);

                        var destState = new NexusDownloader.State
                        {
                            Game = nexusState.Game,
                            ModID = nexusState.ModID,
                            FileID = file.file_id,
                            Name = file.category_name,
                        };
                        var existingState = await SQL.DownloadStateByPrimaryKey(destState.PrimaryKeyString);

                        Hash destHash = default;
                        if (existingState != null)
                        {
                            destHash = existingState.Hash;
                        }

                        var patch = new SqlService.ArchivePatch
                        {
                            SrcHash = archive.Hash, SrcState = archive.State, DestHash = destHash, DestState = destState,
                        };

                        await SQL.UpsertArchivePatch(patch);
                        BeginPatching(patch);
                        break;
                    }
                    case HTTPDownloader.State httpState:
                    {
                        var indexJob = new IndexJob {Archive = new Archive {State = httpState}};
                        await indexJob.Execute(SQL, _settings);

                        var patch = new SqlService.ArchivePatch
                        {
                            SrcHash = archive.Hash,
                            DestHash = indexJob.DownloadedHash,
                            SrcState = archive.State,
                            DestState = archive.State,
                        };
                        await SQL.UpsertArchivePatch(patch);
                        BeginPatching(patch);
                        break;
                    }
                }

                return (archive, ArchiveStatus.InValid);
            }
            catch (Exception)
            {
                return (archive, ArchiveStatus.InValid);
            }
        }

        
        private void BeginPatching(SqlService.ArchivePatch patch)
        {
            Task.Run(async () =>
            {
                if (patch.DestHash == default)
                {
                    patch.DestHash = await DownloadAndHash(patch.DestState);
                }

                patch.SrcDownload = _settings.PathForArchive(patch.SrcHash).RelativeTo(_settings.ArchivePath);
                patch.DestDownload = _settings.PathForArchive(patch.DestHash).RelativeTo(_settings.ArchivePath);

                if (patch.SrcDownload == default || patch.DestDownload == default)
                {
                    throw new InvalidDataException("Src or Destination files do not exist");
                }

                var result = await PatchArchive(patch);


            });
        }
        
        public static AbsolutePath CdnPath(SqlService.ArchivePatch patch)
        {
            return $"updates/{patch.SrcHash.ToHex()}_{patch.DestHash.ToHex()}".RelativeTo(AbsolutePath.EntryPoint);
        }
        private async Task<bool> PatchArchive(SqlService.ArchivePatch patch)
        {
            if (patch.SrcHash == patch.DestHash)
                return true;

            Utils.Log($"Creating Patch ({patch.SrcHash} -> {patch.DestHash})");
            var cdnPath = CdnPath(patch);
            cdnPath.Parent.CreateDirectory();
            
            if (cdnPath.Exists)
                return true;

            Utils.Log($"Calculating Patch ({patch.SrcHash} -> {patch.DestHash})");
            await using var fs = cdnPath.Create();
            await using (var srcStream = patch.SrcDownload.RelativeTo(_settings.ArchivePath).OpenRead())
            await using (var destStream = patch.DestDownload.RelativeTo(_settings.ArchivePath).OpenRead())
            await using (var sigStream = cdnPath.WithExtension(Consts.OctoSig).Create())
            {
                OctoDiff.Create(destStream, srcStream, sigStream, fs);
            }
            fs.Position = 0;
            
            Utils.Log($"Uploading Patch ({patch.SrcHash} -> {patch.DestHash})");

            int retries = 0;
            
            if (_settings.BunnyCDN_User == "TEST" && _settings.BunnyCDN_Password == "TEST")
            {
                return true;
            }
            
            TOP:
            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(_settings.BunnyCDN_User, _settings.BunnyCDN_Password);
                await client.ConnectAsync();
                try
                {
                    await client.UploadAsync(fs, cdnPath.RelativeTo(AbsolutePath.EntryPoint).ToString(), progress: new UploadToCDN.Progress(cdnPath.FileName));
                }
                catch (Exception ex)
                {
                    if (retries > 10) throw;
                    Utils.Log(ex.ToString());
                    Utils.Log("Retrying FTP Upload");
                    retries++;
                    goto TOP;
                }
            }

            patch.CDNPath = new Uri($"https://wabbajackpush.b-cdn.net/{cdnPath}");
            await SQL.UpsertArchivePatch(patch);
            
            return true;
        }

        private async Task<Hash> DownloadAndHash(AbstractDownloadState state)
        {
            var indexJob = new IndexJob();
            await indexJob.Execute(SQL, _settings);
            return indexJob.DownloadedHash;
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
