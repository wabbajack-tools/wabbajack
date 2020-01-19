using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using GraphQLParser.AST;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.GraphQL
{
    public class Query : ObjectGraphType
    {
        public Query(DBContext db)
        {
            Field<ListGraphType<JobType>>("unfinishedJobs", resolve: context =>
            {
                var data =  db.Jobs.AsQueryable().Where(j => j.Ended == null).ToList();
                return data;
            });

            FieldAsync<ListGraphType<ModListStatusType>>("modLists",
                arguments: new QueryArguments(new QueryArgument<ArchiveEnumFilterType>
                {
                    Name = "filter", Description = "Filter lists to those that only have these archive classifications"
                }),
                resolve: async context =>
                {
                    var arg = context.GetArgument<string>("filter");
                    var lists = db.ModListStatus.AsQueryable();
                    switch (arg)
                    {
                        case "FAILED":
                            lists = lists.Where(l => l.DetailedStatus.HasFailures);
                            break;
                        case "PASSED":
                            lists = lists.Where(a => !a.DetailedStatus.HasFailures);
                            break;
                        default:
                            break;
                    }

                    return await lists.ToListAsync();
                });
            
            FieldAsync<ListGraphType<JobType>>("job",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> {Name = "id", Description = "Id of the Job"}),
                resolve: async context =>
                {
                    var id = context.GetArgument<string>("id");
                    var data = await db.Jobs.AsQueryable().Where(j => j.Id == id).ToListAsync();
                    return data;
                });
            
            FieldAsync<ListGraphType<UploadedFileType>>("uploadedFiles",
                resolve: async context =>
                {
                    var data = await db.UploadedFiles.AsQueryable().ToListAsync();
                    return data;
                });

            FieldAsync<ListGraphType<MetricResultType>>("dailyUniqueMetrics",
                arguments: new QueryArguments(
                    new QueryArgument<MetricEnum> {Name = "metric_type", Description = "The grouping of metric data to query"}
                    ),
                resolve: async context =>
                {
                    var group = context.GetArgument<string>("metric_type");
                    return await Metric.Report(db, group);
                });
        }
    }
}
