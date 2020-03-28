using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using System.IO;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class EnqueueAllGameFiles : AJobPayload, IBackEndJob
    {
        public override string Description { get => $"Enqueue all game files for indexing"; }
        public override async Task<JobResult> Execute(DBContext db, SqlService sql, AppSettings settings)
        {
            using (var queue = new WorkQueue(4))
            {
                Utils.Log($"Indexing game files");
                var states = GameRegistry.Games.Values
                    .Where(game => game.GameLocation() != null && game.MainExecutable != null)
                    .SelectMany(game => game.GameLocation().Value.EnumerateFiles()
                        .Select(file => new GameFileSourceDownloader.State
                        {
                            Game = game.Game,
                            GameVersion = game.InstalledVersion,
                            GameFile = file.RelativeTo(game.GameLocation().Value),
                        }))
                    .ToList();
                
                var pks = states.Select(s => s.PrimaryKeyString).Distinct().ToArray();
                Utils.Log($"Found {pks.Length} archives to cross-reference with the database");

                var found = (await db.DownloadStates
                    .AsQueryable().Where(s => pks.Contains(s.Key))
                    .Select(s => s.Key)
                    .ToListAsync())
                    .ToDictionary(s => s);

                states = states.Where(s => !found.ContainsKey(s.PrimaryKeyString)).ToList();
                Utils.Log($"Found {states.Count} archives to index");

                await states.PMap(queue, async state =>
                {
                    var path = state.Game.MetaData().GameLocation().Value.Combine(state.GameFile);
                    Utils.Log($"Hashing Game file {path}");
                    try
                    {
                        state.Hash = await path.FileHashAsync();
                    }
                    catch (IOException)
                    {
                        Utils.Log($"Unable to hash {path}");
                    }
                });

                var with_hash = states.Where(state => state.Hash != null).ToList();
                Utils.Log($"Inserting {with_hash.Count} jobs.");
                var jobs = states.Select(state => new IndexJob {Archive = new Archive {Name = state.GameFile.FileName.ToString(), State = state}})
                    .Select(j => new Job {Payload = j, RequiresNexus = j.UsesNexus})
                    .ToList();

                if (jobs.Count > 0)
                    await db.Jobs.InsertManyAsync(jobs);                
                
                return JobResult.Success();
            }
            
        }
    }
}
