using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/jobs")]
    public class Jobs : AControllerBase<Jobs>
    {
        public Jobs(ILogger<Jobs> logger, SqlService sql) : base(logger, sql)
        {
        }

        [HttpGet]
        [Route("enqueue_job/{JobName}")]
        public async Task<long> EnqueueJob(string JobName)
        {
            var jobtype = AJobPayload.NameToType[JobName];
            var job = new Job{Priority = Job.JobPriority.High, Payload = (AJobPayload)jobtype.GetConstructor(new Type[0]).Invoke(new object[0])};
            await SQL.EnqueueJob(job);
            return job.Id;
        }
    }
}
