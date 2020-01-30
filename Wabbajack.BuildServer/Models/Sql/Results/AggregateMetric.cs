using System;

namespace Wabbajack.BuildServer.Model.Models.Results
{
    public class AggregateMetric
    {
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public int Count { get; set; }
    }
}
