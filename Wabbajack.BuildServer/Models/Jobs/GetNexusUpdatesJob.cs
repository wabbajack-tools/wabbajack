using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using MongoDB.Driver;
using Newtonsoft.Json;


namespace Wabbajack.BuildServer.Models.Jobs
{
    public class GetNexusUpdatesJob : AJobPayload
    {
        public override string Description => "Poll the Nexus for updated mods, and clean any references to those mods";

        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            var api = await NexusApiClient.Get();
            
            var gameTasks = GameRegistry.Games.Values
                .Where(game => game.NexusName != null)
                .Select(async game =>
                {
                    var mods = await api.Get<List<NexusUpdateEntry>>(
                        $"https://api.nexusmods.com/v1/games/{game.NexusName}/mods/updated.json?period=1m");
                    
                    var entry = new NexusCacheData<List<NexusUpdateEntry>>();
                    entry.Game = game.NexusName;
                    entry.Path = $"/v1/games/{game.NexusName}/mods/updated.json?period=1m";
                    entry.Data = mods;

                    await entry.Upsert(db.NexusUpdates);

                    return (game, mods);
                })
                .Select(async rTask =>
                {
                    var (game, mods) = await rTask;
                    return mods.Select(mod => new { game = game, mod = mod });
                }).ToList();

            Utils.Log($"Getting update list for {gameTasks.Count} games");

            var purge = (await Task.WhenAll(gameTasks))
                .SelectMany(i => i)
                .ToList();

            Utils.Log($"Found {purge.Count} updated mods in the last month");
            using (var queue = new WorkQueue())
            {
                var collected = purge.Select(d =>
                {
                    var a = d.mod.LatestFileUpdate.AsUnixTime();
                    // Mod activity could hide files
                    var b = d.mod.LastestModActivity.AsUnixTime();

                    return new {Game = d.game.NexusName, Date = (a > b ? a : b), ModId = d.mod.ModId.ToString()};
                });
                    
                var purged = await collected.PMap(queue, async t =>
                {
                    var resultA = await db.NexusModInfos.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultB = await db.NexusModFiles.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultC = await db.NexusFileInfos.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);

                    return resultA.DeletedCount + resultB.DeletedCount + resultC.DeletedCount;
                });

                Utils.Log($"Purged {purged.Sum()} cache entries");
            }

            return JobResult.Success();
        }
        

    }
}
