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
using MongoDB.Driver;
using MongoDB.Driver.Linq;
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

        public ModlistUpdater(ILogger<ModlistUpdater> logger, DBContext db, SqlService sql, AppSettings settings) : base(logger, db, sql)
        {
            _settings = settings;
            _sql = sql;
        }

        [HttpGet]
        [Authorize]
        [Route("/delete_updates")]
        public async Task<IActionResult> DeleteUpdates()
        {
            var lists = await Db.ModListStatus.AsQueryable().ToListAsync();
            var archives = lists.SelectMany(list => list.DetailedStatus.Archives)
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

            return Ok(new {Save = toSave.ToArray(), Delete = toDelete.ToArray()}.ToJSON(prettyPrint:true));
        }

        [HttpGet]
        [Route("/alternative/{xxHash}")]
        public async Task<IActionResult> GetAlternative(string xxHash)
        {
            var startingHash = Hash.FromHex(xxHash);
            Utils.Log($"Alternative requested for {startingHash}");
            await Metric("requested_upgrade", startingHash.ToString());

            var state = await Db.DownloadStates.AsQueryable()
                .Where(s => s.Hash == startingHash)
                .Where(s => s.State is NexusDownloader.State)
                .OrderByDescending(s => s.LastValidationTime).FirstOrDefaultAsync();

            if (state == null)
                return NotFound("Original state not found");

            var nexusState = state.State as NexusDownloader.State;
            var nexusGame = GameRegistry.GetByFuzzyName(nexusState.GameName).NexusName;
            var mod_files = await Db.NexusModFiles.AsQueryable()
                .Where(f => f.Game == nexusGame && f.ModId == nexusState.ModID)
                .ToListAsync();

            if (mod_files.SelectMany(f => f.Data.files)
                .Any(f => f.category_name != null && f.file_id.ToString() == nexusState.FileID))
            {
                await Metric("not_required_upgrade", startingHash.ToString());
                return BadRequest("Upgrade Not Required");
            }

            Utils.Log($"Found original, looking for alternatives to {startingHash}");
            var newArchive = await FindAlternatives(nexusState, startingHash);
            if (newArchive == null)
            {
                return NotFound("No alternative available");
            }

            Utils.Log($"Found {newArchive.State.PrimaryKeyString} {newArchive.Name} as an alternative to {startingHash}");
            if (newArchive.Hash == Hash.Empty)
            {
                Db.Jobs.InsertOne(new Job
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
                return Accepted("Enqueued for Processing");
            }

            if (startingHash == newArchive.Hash)
                return NotFound("End hash same as old hash");

            if (!PatchArchive.CdnPath(startingHash, newArchive.Hash).Exists)
            {
                Db.Jobs.InsertOne(new Job
                {
                    Priority = Job.JobPriority.High,
                    Payload = new PatchArchive
                    {
                        Src = startingHash,
                        DestPK = newArchive.State.PrimaryKeyString
                    }
                });
            }
            return Ok(newArchive.ToJSON());
        }

        private async Task<Archive> FindAlternatives(NexusDownloader.State state, Hash srcHash)
        {
            var origSize = _settings.PathForArchive(srcHash).Size;
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            var allMods = await api.GetModFiles(GameRegistry.GetByFuzzyName(state.GameName).Game,
                int.Parse(state.ModID));
            var archive = allMods.files.Where(m => !string.IsNullOrEmpty(m.category_name))
                .OrderBy(s => Math.Abs((long)s.size - origSize))
                .Select(s => new Archive {
                    Name = s.file_name,
                    Size = (long)s.size,
                    State = new NexusDownloader.State 
                    {
                    GameName = state.GameName, 
                    ModID = state.ModID, 
                    FileID = s.file_id.ToString()
                }}).FirstOrDefault();

            if (archive == null)
            {
                Utils.Log($"No alternative for {srcHash}");
                return null;
            }
            
            Utils.Log($"Found alternative for {srcHash}");
            
            var indexed = await Db.DownloadStates.AsQueryable().Where(s => s.Key == archive.State.PrimaryKeyString).FirstOrDefaultAsync();

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
