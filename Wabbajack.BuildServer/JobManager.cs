using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;

namespace Wabbajack.BuildServer
{
    public class JobManager
    {
        protected readonly ILogger<JobManager> Logger;
        protected readonly DBContext Db;
        protected readonly AppSettings Settings;

        public JobManager(ILogger<JobManager> logger, DBContext db, AppSettings settings)
        {
            Db = db;
            Logger = logger;
            Settings = settings;
        }

        public void StartJobRunners()
        {
            for (var idx = 0; idx < 2; idx++)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var job = await Job.GetNext(Db);
                            if (job == null)
                            {
                                await Task.Delay(5000);
                                continue;
                            }

                            Logger.Log(LogLevel.Information, $"Starting Job: {job.Payload.Description}");
                            JobResult result;
                            try
                            {
                                result = await job.Payload.Execute(Db, Settings);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(LogLevel.Error, ex, $"Error while running job: {job.Payload.Description}");
                                result = JobResult.Error(ex);
                            }

                            await Job.Finish(Db, job, result);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error, ex, $"Error getting or updating Job");

                        }
                    }
                });
            }
        }
        
        public async Task JobScheduler()
        {
            Utils.LogMessages.Subscribe(msg => Logger.Log(LogLevel.Information, msg.ToString()));
            while (true)
            {
                await KillOrphanedJobs();
                await PollNexusMods();
                await Task.Delay(10000);
            }
        }

        private async Task KillOrphanedJobs()
        {
            try
            {
                var started = await Db.Jobs.AsQueryable()
                    .Where(j => j.Started != null && j.Ended == null)
                    .ToListAsync();
                foreach (var job in started)
                {
                    var runtime = DateTime.Now - job.Started;
                    if (runtime > TimeSpan.FromMinutes(30))
                    {
                        await Job.Finish(Db, job, JobResult.Error(new Exception($"Timeout after {runtime.Value.TotalMinutes}")));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex, "Error in JobScheduler when scheduling GetNexusUpdatesJob");
            }
        }

        private async Task PollNexusMods()
        {
            try
            {
                var updaters = await Db.Jobs.AsQueryable()
                    .Where(j => j.Payload is GetNexusUpdatesJob)
                    .Where(j => j.Started == null)
                    .OrderBy(j => j.Created)
                    .ToListAsync();
                if (updaters.Count == 0)
                {
                    await Db.Jobs.InsertOneAsync(new Job
                    {
                        Payload = new GetNexusUpdatesJob()
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex, "Error in JobScheduler when scheduling GetNexusUpdatesJob");
            }
        }
    }
}
