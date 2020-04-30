using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.BuildServer.BackendServices;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.BuildServer
{
    public class JobManager
    {
        protected readonly ILogger<JobManager> Logger;
        protected readonly AppSettings Settings;
        protected SqlService Sql;

        public JobManager(ILogger<JobManager> logger, SqlService sql, AppSettings settings)
        {
            Logger = logger;
            Settings = settings;
            Sql = sql;
        }


        public void StartJobRunners()
        {
            if (!Settings.JobRunner) return;
            for (var idx = 0; idx < Settings.MaxJobs; idx++)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var job = await Sql.GetJob();
                            if (job == null)
                            {
                                await Task.Delay(5000);
                                continue;
                            }

                            Logger.Log(LogLevel.Information, $"Starting job: {job.Payload.Description}");
                            try
                            {
                                job.Result = await job.Payload.Execute(Sql, Settings);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(LogLevel.Error, ex, $"Error while running job: {job.Payload.Description}");
                                job.Result = JobResult.Error(ex);
                            }

                            await Sql.FinishJob(job);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error, ex, $"Error getting or updating job");

                        }
                    }
                });
            }
        }
        
        public async Task JobScheduler()
        {
            Utils.LogMessages.Subscribe(msg => Logger.Log(LogLevel.Information, msg.ToString()));
            Utils.LogMessages.Subscribe(Heartbeat.AddToLog);
            Utils.LogMessages.OfType<IUserIntervention>().Subscribe(u => u.Cancel());
            if (!Settings.JobScheduler) return;

            var token = new CancellationTokenSource();
            var task = RunNexusCacheLoop();
            var listIngest = (new ListIngest(Sql, Settings)).RunLoop(token.Token);
            var nonNexus = (new ValidateNonNexusArchives(Sql, Settings)).RunLoop(token.Token);
            
            while (true)
            {
                await KillOrphanedJobs();
                await ScheduledJob<GetNexusUpdatesJob>(TimeSpan.FromHours(1), Job.JobPriority.High);
                //await ScheduledJob<UpdateModLists>(TimeSpan.FromMinutes(30), Job.JobPriority.High);
                //await ScheduledJob<EnqueueAllArchives>(TimeSpan.FromHours(2), Job.JobPriority.Low);
                //await ScheduledJob<EnqueueAllGameFiles>(TimeSpan.FromHours(24), Job.JobPriority.High);
                await ScheduledJob<IndexDynDOLOD>(TimeSpan.FromHours(1), Job.JobPriority.Normal);
                await Task.Delay(10000);
            }
        }

        private async Task RunNexusCacheLoop()
        {
            while (true)
            {
                await GetNexusUpdatesJob.UpdateNexusCacheFast(Sql);
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }


        private async Task KillOrphanedJobs()
        {
            try
            {
                var started = await Sql.GetRunningJobs();
                foreach (var job in started)
                {
                    var runtime = DateTime.Now - job.Started;
                    
                    if (!(runtime > TimeSpan.FromMinutes(30))) continue;

                    job.Result = JobResult.Error(new Exception($"Timeout after {runtime.Value.TotalMinutes}"));
                    await Sql.FinishJob(job);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex, "Error in JobScheduler when scheduling KillOrphanedJobs");
            }
        }
        
        private async Task ScheduledJob<T>(TimeSpan span, Job.JobPriority priority) where T : AJobPayload, new()
        {
            if (!Settings.RunBackEndJobs && typeof(T).ImplementsInterface(typeof(IBackEndJob))) return;
            if (!Settings.RunFrontEndJobs && typeof(T).ImplementsInterface(typeof(IFrontEndJob))) return;
            try
            {
                var jobs = (await Sql.GetAllJobs(span))
                    .Where(j => j.Payload is T)
                    .OrderByDescending(j => j.Created)
                    .Take(10);

                foreach (var job in jobs)
                {
                    if (job.Started == null || job.Ended == null) return;
                    if (DateTime.UtcNow - job.Ended < span) return;
                }
                await Sql.EnqueueJob(new Job
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
