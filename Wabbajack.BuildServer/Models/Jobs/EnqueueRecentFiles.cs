using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Core.Authentication;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class EnqueueRecentFiles : AJobPayload
    {
        public override string Description => "Enqueue the past days worth of mods for indexing";
        
        private static HashSet<Game> GamesToScan = new HashSet<Game>
        {
            Game.Fallout3, Game.Fallout4, Game.Skyrim, Game.SkyrimSpecialEdition, Game.SkyrimVR, Game.FalloutNewVegas, Game.Oblivion
        };
        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            using (var queue = new WorkQueue())
            {
                var updates = await db.NexusUpdates.AsQueryable().ToListAsync();
                var mods = updates
                    .Where(list => GamesToScan.Contains(GameRegistry.GetByNexusName(list.Game).Game))
                    .SelectMany(list =>
                    list.Data.Where(mod => DateTime.UtcNow - mod.LatestFileUpdate.AsUnixTime() < TimeSpan.FromDays(1))
                        .Select(mod => (list.Game, mod.ModId)));
                var mod_files = (await mods.PMap(queue, async mod =>
                {
                    var client = await NexusApiClient.Get();
                    try
                    {
                        var files = await client.GetModFiles(GameRegistry.GetByNexusName(mod.Game).Game,
                            (int)mod.ModId);
                        return (mod.Game, mod.ModId, files.files);
                    }
                    catch (Exception)
                    {
                        return default;
                    }
                })).Where(t => t.Game != null).ToList();
                
                var archives = 
                    mod_files.SelectMany(mod => mod.files.Select(file => (mod.Game, mod.ModId, File:file)).Where(f => !string.IsNullOrEmpty(f.File.category_name) ))
                        .Select(tuple =>
                        {
                            var state = new NexusDownloader.State
                            {
                                GameName = tuple.Game, ModID = tuple.ModId.ToString(), FileID = tuple.File.file_id.ToString()
                            };
                            return new Archive {State = state, Name = tuple.File.file_name}; 
                        }).ToList();
                
                Utils.Log($"Found {archives.Count} archives from recent Nexus updates to index");
                var searching = archives.Select(a => a.State.PrimaryKeyString).Distinct().ToArray();

                Utils.Log($"Looking for missing states");
                var knownArchives = (await db.DownloadStates.AsQueryable().Where(s => searching.Contains(s.Key))
                    .Select(d => d.Key).ToListAsync()).ToDictionary(a => a);

                Utils.Log($"Found {knownArchives.Count} pre-existing archives");
                var missing = archives.Where(a => !knownArchives.ContainsKey(a.State.PrimaryKeyString))
                    .DistinctBy(d => d.State.PrimaryKeyString)
                    .ToList();

                Utils.Log($"Found {missing.Count} missing archives, enqueing indexing jobs");

                var jobs = missing.Select(a => new Job {Payload = new IndexJob {Archive = a}, Priority = Job.JobPriority.Low});

                Utils.Log($"Writing jobs to the DB");
                await db.Jobs.InsertManyAsync(jobs, new InsertManyOptions {IsOrdered = false});
                Utils.Log($"Done adding archives for Nexus Updates");
                
                return JobResult.Success();
            }
        }
    }
}
