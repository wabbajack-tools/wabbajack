using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Models;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    public abstract class AControllerBase<T> : ControllerBase
    {
        protected readonly DBContext Db;
        protected readonly ILogger<T> Logger;

        protected AControllerBase(ILogger<T> logger, DBContext db)
        {
            Db = db;
            Logger = logger;
        }
    }
}
