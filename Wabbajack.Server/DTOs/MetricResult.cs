using System.Collections.Generic;

namespace Wabbajack.Server.DTOs
{
    public class MetricResult
    {
        public string SeriesName { get; set; }
        public List<string> Labels { get; set; }
        public List<int> Values { get; set; }
    }
}
