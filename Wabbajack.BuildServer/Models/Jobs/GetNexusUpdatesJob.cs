using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common.Serialization.Json;


namespace Wabbajack.BuildServer.Models.Jobs
{
    [JsonName("GetNexusUpdatesJob")]
    public class GetNexusUpdatesJob : AJobPayload, IFrontEndJob
    {
        public override string Description => "Poll the Nexus for updated mods, and clean any references to those mods";

        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
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

                    return new {Game = d.game.Game, Date = (a > b ? a : b), ModId = d.mod.ModId};
                });
                    
                var purged = await collected.PMap(queue, async t =>
                {
                    var resultA = await sql.DeleteNexusModInfosUpdatedBeforeDate(t.Game, t.ModId, t.Date);
                    var resultB = await sql.DeleteNexusModFilesUpdatedBeforeDate(t.Game, t.ModId, t.Date);
                    return resultA + resultB;
                });

                Utils.Log($"Purged {purged.Sum()} cache entries");
            }

            return JobResult.Success();
        }

        protected override IEnumerable<object> PrimaryKey => new object[0];

        public static DateTime LastNexusSync { get; set; } = DateTime.Now;
        public static async Task<long> UpdateNexusCacheFast(SqlService sql)
        {
            var results = await NexusUpdatesFeeds.GetUpdates();
            NexusApiClient client = null;
            long updated = 0;
            foreach (var result in results)
            {
                try
                {
                    var purgedMods =
                        await sql.DeleteNexusModFilesUpdatedBeforeDate(result.Game, result.ModId, result.TimeStamp);
                    var purgedFiles =
                        await sql.DeleteNexusModInfosUpdatedBeforeDate(result.Game, result.ModId, result.TimeStamp);

                    var totalPurged = purgedFiles + purgedMods;
                    if (totalPurged > 0)
                        Utils.Log($"Purged {totalPurged} cache items");

                    if (await sql.GetNexusModInfoString(result.Game, result.ModId) != null) continue;

                    // Lazily create the client
                    client ??= await NexusApiClient.Get();

                    // Cache the info
                    var files = await client.GetModFiles(result.Game, result.ModId, false);
                    await sql.AddNexusModFiles(result.Game, result.ModId, result.TimeStamp, files);

                    var modInfo = await client.GetModInfo(result.Game, result.ModId);
                    await sql.AddNexusModInfo(result.Game, result.ModId, result.TimeStamp, modInfo);
                    updated++;
                }
                catch (Exception ex)
                {
                    Utils.Log($"Failed Nexus update for {result.Game} - {result.ModId} - {result.TimeStamp}");
                }

            }

            if (updated > 0) 
                Utils.Log($"Primed {updated} nexus cache entries");

            LastNexusSync = DateTime.Now;
            return updated;
        }
        

    }
}
