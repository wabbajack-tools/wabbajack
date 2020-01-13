using System;
using System.Linq;
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
                await ScheduledJob<GetNexusUpdatesJob>(TimeSpan.FromHours(2), Job.JobPriority.High);
                await ScheduledJob<UpdateModLists>(TimeSpan.FromMinutes(30), Job.JobPriority.High);
                await ScheduledJob<EnqueueAllArchives>(TimeSpan.FromHours(2), Job.JobPriority.Low);
                await ScheduledJob<EnqueueAllGameFiles>(TimeSpan.FromHours(24), Job.JobPriority.High);
                await ScheduledJob<EnqueueRecentFiles>(TimeSpan.FromHours(6), Job.JobPriority.Low);
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
                Logger.Log(LogLevel.Error, ex, "Error in JobScheduler when scheduling KillOrphanedJobs");
            }
        }
        
        private async Task ScheduledJob<T>(TimeSpan span, Job.JobPriority priority) where T : AJobPayload, new()
        {
            try
            {
                var jobs = await Db.Jobs.AsQueryable()
                    .Where(j => j.Payload is T)
                    .OrderByDescending(j => j.Created)
                    .Take(10)
                    .ToListAsync();

                foreach (var job in jobs)
                {
                    if (job.Started == null || job.Ended == null) return;
                    if (DateTime.Now - job.Ended < span) return;
                }
                await Db.Jobs.InsertOneAsync(new Job
                {
                    Priority = priority,
                    Payload = new T()
                });
            }
            catch (Exception ex)
            {
                
                Logger.Log(LogLevel.Error, ex, $"Error in JobScheduler when scheduling {typeof(T).Name}");
            }
        }

    }
}
