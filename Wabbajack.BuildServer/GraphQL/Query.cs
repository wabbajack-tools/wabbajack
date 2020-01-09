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
            
            FieldAsync<ListGraphType<JobType>>("job",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> {Name = "id", Description = "Id of the Job"}),
                resolve: async context =>
                {
                    var id = Guid.Parse(context.GetArgument<string>("id"));
                    var data = await db.Jobs.AsQueryable().Where(j => j.Id == id).ToListAsync();
                    return data;
                });

            Field<VirtualFileType> ("indexedFileTree",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> {Name = "hash", Description = "Hash of the Job"}),
                resolve: context =>
                {
                    var hash = context.GetArgument<string>("hash");
                    var data = db.IndexedFiles.AsQueryable().Where(j => j.Hash == hash).FirstOrDefault();
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
