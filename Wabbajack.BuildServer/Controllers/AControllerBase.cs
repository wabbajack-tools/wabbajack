using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using GraphQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Org.BouncyCastle.Ocsp;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    public abstract class AControllerBase<T> : ControllerBase
    {
        protected readonly DBContext Db;
        protected readonly ILogger<T> Logger;
        protected readonly SqlService SQL;

        protected AControllerBase(ILogger<T> logger, DBContext db, SqlService sql)
        {
            Db = db;
            Logger = logger;
            SQL = sql;
        }

        
        protected async Task Metric(string verb, string subject)
        {
            await SQL.IngestMetric(new Metric
            {
                MetricsKey = Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault(),
                Subject = subject,
                Action = verb,
                Timestamp = DateTime.UtcNow
            });
        }


    }
}
