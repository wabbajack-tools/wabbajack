using System.Collections.Generic;
using GraphQL.Types;

namespace Wabbajack.BuildServer.GraphQL
{
    public class MetricEnum : EnumerationGraphType
    {
        public MetricEnum()
        {
            Name = "MetricType";
            Description = "The metric grouping";
            AddValue("BEGIN_INSTALL", "Installation of a modlist started", "begin_install");
            AddValue("FINISHED_INSTALL", "Installation of a modlist finished", "finish_install");
            AddValue("BEGIN_DOWNLOAD", "Downloading of a modlist begain started", "downloading");
        }
    }

    public class MetricResultType : ObjectGraphType<MetricResult>
    {
        public MetricResultType()
        {
            Name = "MetricResult";
            Description =
                "A single line of data from a metrics graph. For example, the number of unique downloads each day.";
            Field(x => x.SeriesName).Description("The name of the data series");
            Field(x => x.Labels).Description("The name for each plot of data (for example the date for each value");
            Field(x => x.Values).Description("The value for each plot of data");
        }
    }
    public class MetricResult
    {
        public string SeriesName { get; set; }
        public List<string> Labels { get; set; }
        public List<int> Values { get; set; }
    }
}
