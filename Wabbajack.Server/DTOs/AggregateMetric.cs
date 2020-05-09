using System;

namespace Wabbajack.Server.DTOs
{
    public class AggregateMetric
    {
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public int Count { get; set; }
    }
}
