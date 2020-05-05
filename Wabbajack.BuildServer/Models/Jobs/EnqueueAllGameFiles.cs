using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using System.IO;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.BuildServer.Models.Jobs
{
    [JsonName("EnqueueAllGameFiles")]
    public class EnqueueAllGameFiles : AJobPayload, IBackEndJob
    {
        public override string Description { get => $"Enqueue all game files for indexing"; }
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            using (var queue = new WorkQueue(4))
            {
                Utils.Log($"Finding game files to Index game files");
                var states = GameRegistry.Games.Values
                    .Where(game => game.TryGetGameLocation() != default && game.MainExecutable != null)
                    .SelectMany(game => game.GameLocation().EnumerateFiles()
                        .Select(file => new GameFileSourceDownloader.State(game.InstalledVersion)
                        {
                            Game = game.Game,
                            GameFile = file.RelativeTo(game.GameLocation()),
                        }))
                    .ToList();
                
                var pks = states.Select(s => s.PrimaryKeyString).ToHashSet();
                Utils.Log($"Found {pks.Count} archives to cross-reference with the database");

                var notFound = await sql.FilterByExistingPrimaryKeys(pks);
                
                states = states.Where(s => notFound.Contains(s.PrimaryKeyString)).ToList();
                Utils.Log($"Found {states.Count} archives to index");

                await states.PMap(queue, async state =>
                {
                    var path = state.Game.MetaData().GameLocation().Combine(state.GameFile);
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

                var with_hash = states.Where(state => state.Hash != default).ToList();
                Utils.Log($"Inserting {with_hash.Count} jobs.");
                var jobs = states.Select(state => new IndexJob {Archive = new Archive(state) { Name = state.GameFile.FileName.ToString()}})
                    .Select(j => new Job {Payload = j, RequiresNexus = j.UsesNexus})
                    .ToList();

                foreach (var job in jobs)
                    await sql.EnqueueJob(job);
                
                return JobResult.Success();
            }
        }

        protected override IEnumerable<object> PrimaryKey => new object[0];
    }
}
