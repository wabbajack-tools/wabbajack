using System;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
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

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/listupdater")]
    public class ModlistUpdater : AControllerBase<ModlistUpdater>
    {
        private AppSettings _settings;
        private SqlService _sql;

        public ModlistUpdater(ILogger<ModlistUpdater> logger, DBContext db, SqlService sql, AppSettings settings) : base(logger, db)
        {
            _settings = settings;
            _sql = sql;
        }

        [HttpGet]
        [Route("/alternative/{xxHash}")]
        public async Task<IActionResult> GetAlternative(string xxHash)
        {
            var startingHash = xxHash.FromHex().ToBase64();
            Utils.Log($"Alternative requested for {startingHash}");

            var state = await Db.DownloadStates.AsQueryable()
                .Where(s => s.Hash == startingHash)
                .Where(s => s.State is NexusDownloader.State)
                .OrderByDescending(s => s.LastValidationTime).FirstOrDefaultAsync();

            if (state == null)
                return NotFound("Original state not found");

            var nexusState = state.State as NexusDownloader.State;

            Utils.Log($"Found original, looking for alternatives to {startingHash}");
            var newArchive = await FindAlternatives(nexusState, startingHash);
            if (newArchive == null)
            {
                return NotFound("No alternative available");
            }

            Utils.Log($"Found {newArchive.State.PrimaryKeyString} as an alternative to {startingHash}");
            if (newArchive.Hash == null)
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

            if (!AlphaFile.Exists(PatchArchive.CdnPath(startingHash, newArchive.Hash)))
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

        private async Task<Archive> FindAlternatives(NexusDownloader.State state, string srcHash)
        {
            var origSize = AlphaFile.GetSize(_settings.PathForArchive(srcHash));
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
