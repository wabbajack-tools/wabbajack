using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using GraphQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

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
