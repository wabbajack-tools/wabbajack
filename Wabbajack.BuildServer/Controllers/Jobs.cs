using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/jobs")]
    public class Jobs : AControllerBase<Jobs>
    {
        public Jobs(ILogger<Jobs> logger, DBContext db) : base(logger, db)
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
    }
}
