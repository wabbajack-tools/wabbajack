using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/jobs")]
    public class Jobs : AControllerBase<Jobs>
    {
        public Jobs(ILogger<Jobs> logger, DBContext db, SqlService sql) : base(logger, db, sql)
        {
        }

        [HttpGet]
        [Route("unfinished")]
        public async Task<IEnumerable<Job>> GetUnfinished()
        {
            return await Db.Jobs.AsQueryable()
                .Where(j => j.Ended == null)
                .OrderByDescending(j => j.Priority)
                .ToListAsync();
        }

        [HttpGet]
        [Route("enqueue_job/{JobName}")]
        public async Task<string> EnqueueJob(string JobName)
        {
            var jobtype = AJobPayload.NameToType[JobName];
            var job = new Job{Priority = Job.JobPriority.High, Payload = (AJobPayload)jobtype.GetConstructor(new Type[0]).Invoke(new object?[0])};
            await Db.Jobs.InsertOneAsync(job);
            return job.Id;
        }
    }
}
