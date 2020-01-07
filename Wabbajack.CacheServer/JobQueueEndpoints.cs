using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Policy;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nancy;
using Nettle;
using Wabbajack.CacheServer.DTOs.JobQueue;
using Wabbajack.CacheServer.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.CacheServer
{
    public class JobQueueEndpoints : NancyModule
    {
        public JobQueueEndpoints() : base ("/jobs")
        {
            Get("/", HandleListJobs);
            Get("/enqueue_curated_for_indexing", HandleEnqueueAllCurated);
            Get("/enqueue_game_files_for_indexing", HandleEnqueueAllGameFiles);
        }

        private readonly Func<object, string> HandleListJobsTemplate = NettleEngine.GetCompiler().Compile(@"
                <html><head/><body>

                <h2>Jobs - {{$.jobs.Count}} Pending</h2>
                <h3>{{$.time}}</h3>
                <ol>
                {{each $.jobs}}
                    <li>{{$.Description}}</li>
                {{/each}}
                </ol>

                <script>
                setTimeout(function() { location.reload();}, 10000);
                </script>

                </body></html>");

        private async Task<Response> HandleListJobs(object arg)
        {
            var jobs = await Server.Config.JobQueue.Connect()
                .AsQueryable<Job>()
                .Where(j => j.Ended == null)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Created)
                .ToListAsync();

            var response = (Response)HandleListJobsTemplate(new {jobs, time = DateTime.Now});
            response.ContentType = "text/html";
            return response;
        }


        private async Task<string> HandleEnqueueAllCurated(object arg)
        {
            var states = await Server.Config.ListValidation.Connect()
                .AsQueryable()
                .SelectMany(lst => lst.DetailedStatus.Archives)
                .Select(a => a.Archive)
                .ToListAsync();

            var jobs = states.Select(state => new IndexJob {Archive = state})
                .Select(j => new Job {Payload = j, RequiresNexus = j.UsesNexus})
                .ToList();

            if (jobs.Count > 0)
                await Server.Config.JobQueue.Connect().InsertManyAsync(jobs);

            return $"Enqueued {states.Count} jobs";
        }

        private async Task<string> HandleEnqueueAllGameFiles(object arg)
        {
            using (var queue = new WorkQueue(4))
            {
                var states = GameRegistry.Games.Values
                    .Where(game => game.GameLocation() != null && game.MainExecutable != null)
                    .SelectMany(game => Directory.EnumerateFiles(game.GameLocation(), "*", SearchOption.AllDirectories)
                        .Select(file => new GameFileSourceDownloader.State
                        {
                            Game = game.Game,
                            GameVersion = game.InstalledVersion,
                            GameFile = file.RelativeTo(game.GameLocation()),
                        }))
                    .ToList();

                await states.PMap(queue, state =>
                {
                    state.Hash = Path.Combine(state.Game.MetaData().GameLocation(), state.GameFile).FileHash();
                });
                
                var jobs = states.Select(state => new IndexJob {Archive = new Archive {Name = Path.GetFileName(state.GameFile), State = state}})
                    .Select(j => new Job {Payload = j, RequiresNexus = j.UsesNexus})
                    .ToList();

                if (jobs.Count > 0)
                    await Server.Config.JobQueue.Connect().InsertManyAsync(jobs);                
                
                return $"Enqueued {states.Count} Jobs";
            }
        }

        public static async Task StartJobQueue()
        {
            foreach (var task in Enumerable.Range(0, 4))
            {
                var tsk = StartJobQueueInner();
            }
        }

        private static async Task StartJobQueueInner()
        {
            while (true)
            {
                try
                {
                    var job = await Job.GetNext();
                    if (job == null)
                    {
                        await Task.Delay(5000);
                        continue;
                    }

                    var result = await job.Payload.Execute();
                    await Job.Finish(job, result);
                }
                catch (Exception ex)
                {

                }

            }
        }
    }
}
