using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/metrics")]
    public class Metrics : AControllerBase<Metrics>
    {
        [HttpGet]
        [Route("{Action}/Value")]
        public async Task<string> NewMetric(string Action, string Value)
        {
            
            var date = DateTime.UtcNow;
            await Log(date, Action, Value, Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault());
            return date.ToString();
        }

        public Metrics(ILogger<Metrics> logger, DBContext db) : base(logger, db)
        {
        }
        
        internal async Task Log(DateTime timestamp, string action, string subject, string metricsKey = null)
        {
            var msg = new[] {string.Join("\t", new[]{timestamp.ToString(), metricsKey, action, subject})};
            Utils.Log(msg.First());
            await Db.Metrics.InsertOneAsync(new Metric {Timestamp = timestamp, Action = action, Subject = subject, MetricsKey = metricsKey});
        }
    }
}
