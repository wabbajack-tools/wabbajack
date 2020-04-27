using System.Linq;
using GraphQL.Types;
using Wabbajack.BuildServer.Model.Models;

namespace Wabbajack.BuildServer.GraphQL
{
    public class Query : ObjectGraphType
    {
        public Query(SqlService sql)
        {
            /*
            FieldAsync<ListGraphType<ModListStatusType>>("modLists",
                arguments: new QueryArguments(new QueryArgument<ArchiveEnumFilterType>
                {
                    Name = "filter", Description = "Filter lists to those that only have these archive classifications"
                }),
                resolve: async context =>
                {
                    var arg = context.GetArgument<string>("filter");
                    var lists = await sql.GetDetailedModlistStatuses();
                    switch (arg)
                    {
                        case "FAILED":
                            return lists.Where(l => l.HasFailures);
                        case "PASSED":
                            return lists.Where(l => !l.HasFailures);
                        default:
                            return lists;
                    }

                });
*/
            FieldAsync<ListGraphType<MetricResultType>>("dailyUniqueMetrics",
                arguments: new QueryArguments(
                    new QueryArgument<MetricEnum> {Name = "metric_type", Description = "The grouping of metric data to query"}
                    ),
                resolve: async context =>
                {
                    var group = context.GetArgument<string>("metric_type");
                    var data = (await sql.MetricsReport(group))
                                  .GroupBy(m => m.Subject)
                                  .Select(g => new MetricResult
                                  {
                                      SeriesName = g.Key,
                                      Labels = g.Select(m => m.Date.ToString()).ToList(),
                                      Values = g.Select(m => m.Count).ToList()
                                  });
                    return data;
                });
        }
    }
}
