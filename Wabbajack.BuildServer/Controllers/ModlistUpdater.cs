using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using AlphaFile = Alphaleonis.Win32.Filesystem.File;
using Directory = System.IO.Directory;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/listupdater")]
    public class ModlistUpdater : AControllerBase<ModlistUpdater>
    {
        private AppSettings _settings;
        private SqlService _sql;

        public ModlistUpdater(ILogger<ModlistUpdater> logger, SqlService sql, AppSettings settings) : base(logger, sql)
        {
            _settings = settings;
            _sql = sql;
        }

        [HttpGet]
        [Authorize]
        [Route("/delete_updates")]
        public async Task<IActionResult> DeleteUpdates()
        {
            var lists = await SQL.GetDetailedModlistStatuses();
            var archives = lists.SelectMany(list => list.Archives)
                .Select(a => a.Archive.Hash.ToHex())
                .ToHashSet();

            var toDelete = new List<string>();
            var toSave = new List<string>();
            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(_settings.BunnyCDN_User, _settings.BunnyCDN_Password);
                await client.ConnectAsync();
                
                foreach (var file in Directory.GetFiles("updates"))
                {
                    var relativeName = Path.GetFileName(file);
                    var parts = Path.GetFileName(file).Split('_', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) continue;

                    if (parts[0] == parts[1])
                    {
                        toDelete.Add(relativeName);
                        continue;
                    }

                    if (!archives.Contains(parts[0]))
                        toDelete.Add(relativeName);
                    else
                        toSave.Add(relativeName);
                }

                foreach (var delete in toDelete)
                {
                    Utils.Log($"Deleting update {delete}");
                    if (await client.FileExistsAsync($"updates/{delete}"))
                        await client.DeleteFileAsync($"updates/{delete}");
                    if (AlphaFile.Exists($"updates\\{delete}"))
                        AlphaFile.Delete($"updates\\{delete}");

                }
            }

            return Ok(new {Save = toSave.ToArray(), Delete = toDelete.ToArray()}.ToJson());
        }

        [HttpGet]
        [Route("/alternative/{xxHash}")]
        public async Task<IActionResult> GetAlternative(string xxHash)
        {
            var startingHash = Hash.FromHex(xxHash);
            await Metric("requested_upgrade", startingHash.ToString());

            var archive = await SQL.GetStateByHash(startingHash);

            if (archive == null)
            {
                return NotFound("Original state not found");
            }

            Archive newArchive;
            IActionResult result;
            switch (archive.State)
            {
                case NexusDownloader.State _:
                {
                    (result, newArchive) = await FindNexusAlternative(archive);
                    if (newArchive == null)
                        return result;
                    break;
                }
                case HTTPDownloader.State _:
                    (result, newArchive) = await FindHttpAlternative(archive);
                    if (newArchive == null)
                        return result;
                    break;
                default:
                    return NotFound("No alternative");
            }


            if (newArchive.Hash == Hash.Empty)
            {
                await SQL.EnqueueJob(new Job
                {
                    Payload = new IndexJob
                    {
                        Archive = newArchive
                    },
                    OnSuccess = new Job
                    {
                        Payload = new PatchArchive
                        {
                            Src = startingHash,
                            DestPK = newArchive.State.PrimaryKeyString
                        }
                    }
                });
                Utils.Log($"Enqueued Index and Upgrade for {startingHash} -> {newArchive.State.PrimaryKeyString}");
                return Accepted("Enqueued for Processing");
            }

            if (startingHash == newArchive.Hash)
                return NotFound("End hash same as old hash");

            if (!PatchArchive.CdnPath(startingHash, newArchive.Hash).Exists)
            {
                await SQL.EnqueueJob(new Job
                {
                    Priority = Job.JobPriority.High,
                    Payload = new PatchArchive
                    {
                        Src = startingHash,
                        DestPK = newArchive.State.PrimaryKeyString
                    }
                });
                Utils.Log($"Enqueued Upgrade for {startingHash} -> {newArchive.State.PrimaryKeyString}");
            }
            return Ok(newArchive.ToJson());
        }


        private async Task<(IActionResult, Archive)> FindHttpAlternative(Archive archive)
        {
            try
            {
                var valid = await archive.State.Verify(archive);

                if (valid)
                {
                    return (NotFound("Http file still valid"), null);
                }

                archive.Hash = default;
                archive.Size = 0;
                return (Ok("Index"), archive);
            }
            catch
            {
                Utils.Log($"Http file {archive.Hash} no longer exists");
                return (NotFound("Http file no longer exists"), null);
            }
        }
        private async Task<(IActionResult, Archive)> FindNexusAlternative(Archive archive)
        {
            var nexusState = (NexusDownloader.State)archive.State;
            var nexusGame = nexusState.Game;
            var nexusModFiles = await SQL.GetModFiles(nexusGame, nexusState.ModID);
            if (nexusModFiles == null)
            {
                Utils.Log($"No nexus mod files for {archive.Hash}");
                return (NotFound("No nexus info"), null);
            }
            var mod_files = nexusModFiles.files;

            if (mod_files.Any(f => f.category_name != null && f.file_id == nexusState.FileID))
            {
                Utils.Log($"No available upgrade required for {nexusState.PrimaryKey}");
                await Metric("not_required_upgrade", archive.Hash.ToString());
                return (BadRequest("Upgrade Not Required"), null);
            }

            Utils.Log($"Found original, looking for alternatives to {archive.Hash}");
            var newArchive = await FindNexusAlternative(nexusState, archive.Hash);
            if (newArchive != null)
            {
                return (Ok(newArchive), newArchive);
            }

            Utils.Log($"No available upgrade for {nexusState.PrimaryKey}");
            return (NotFound("No alternative available"), null);

        }

        private async Task<Archive> FindNexusAlternative(NexusDownloader.State state, Hash srcHash)
        {
            var origSize = _settings.PathForArchive(srcHash).Size;
            var api = await NexusApiClient.Get(Request?.Headers["apikey"].FirstOrDefault());
            var allMods = await api.GetModFiles(state.Game, state.ModID);
            var archive = allMods.files.Where(m => !string.IsNullOrEmpty(m.category_name))
                .OrderBy(s => Math.Abs((long)s.size - origSize))
                .Select(s =>
                    new Archive(
                        new NexusDownloader.State
                        {
                            Game = state.Game,
                            ModID = state.ModID,
                            FileID = s.file_id
                        })
                    {
                        Name = s.file_name,
                        Size = (long)s.size,
                    })
                .FirstOrDefault();

            if (archive == null)
            {
                Utils.Log($"No alternative for {srcHash}");
                return null;
            }
            
            Utils.Log($"Found alternative for {srcHash}");
            
            var indexed = await SQL.DownloadStateByPrimaryKey(archive.State.PrimaryKeyString);

            if (indexed == null)
            {
                return archive;
            }

            Utils.Log($"Pre-Indexed alternative {indexed.Hash} found for {srcHash}");
            archive.Hash = indexed.Hash;
            return archive;
        }
    }
}
