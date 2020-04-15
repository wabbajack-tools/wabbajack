using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    public abstract class AControllerBase<T> : ControllerBase
    {
        protected readonly ILogger<T> Logger;
        protected readonly SqlService SQL;

        protected AControllerBase(ILogger<T> logger, SqlService sql)
        {
            Logger = logger;
            SQL = sql;
        }

        
        protected async Task Metric(string verb, string subject)
        {
            await SQL.IngestMetric(new Metric
            {
                MetricsKey = Request?.Headers[Consts.MetricsKeyHeader].FirstOrDefault() ?? "",
                Subject = subject,
                Action = verb,
                Timestamp = DateTime.UtcNow
            });
        }


    }
}
