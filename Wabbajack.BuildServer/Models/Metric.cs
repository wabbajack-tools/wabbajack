using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.BuildServer.GraphQL;
using Wabbajack.BuildServer.Model.Models;


namespace Wabbajack.BuildServer.Models
{
    public class Metric
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string Subject { get; set; }
        public string MetricsKey { get; set; }
    }
}
