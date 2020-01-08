using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/lists")]

    public class ListValidation : AControllerBase<ListValidation>
    {
        public ListValidation(ILogger<ListValidation> logger, DBContext db) : base(logger, db)
        {
        }
        
        [HttpGet]
        [Route("status")]
        public async Task<IList<ModlistSummary>> HandleGetLists()
        {
            return await Db.ModListStatus.AsQueryable().Select(m => m.Summary).ToListAsync();
        }
    }
}
