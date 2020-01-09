using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using MongoDB.Driver;


namespace Wabbajack.BuildServer.Models.Jobs
{
    public class GetNexusUpdatesJob : AJobPayload
    {
        public override string Description => "Poll the nexus for updated mods, and clean any references to those mods";

        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            var api = await NexusApiClient.Get();
            
            var gameTasks = GameRegistry.Games.Values
                .Where(game => game.NexusName != null)
                .Select(async game =>
                {
                    return (game,
                        mods: await api.Get<List<UpdatedMod>>(
                            $"https://api.nexusmods.com/v1/games/{game.NexusName}/mods/updated.json?period=1m"));
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
                var collected = await purge.Select(d =>
                {
                    var a = d.mod.latest_file_update.AsUnixTime();
                    // Mod activity could hide files
                    var b = d.mod.latest_mod_activity.AsUnixTime();

                    return new {Game = d.game.NexusName, Date = (a > b ? a : b), ModId = d.mod.mod_id.ToString()};
                }).PMap(queue, async t =>
                {
                    var resultA = await db.NexusModInfos.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultB = await db.NexusModFiles.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultC = await db.NexusFileInfos.DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);

                    return resultA.DeletedCount + resultB.DeletedCount + resultC.DeletedCount;
                });

                Utils.Log($"Purged {collected.Sum()} cache entries");
            }

            return JobResult.Success();
        }
        
        class UpdatedMod
        {
            public long mod_id;
            public long latest_file_update;
            public long latest_mod_activity;
        }
    }
}
