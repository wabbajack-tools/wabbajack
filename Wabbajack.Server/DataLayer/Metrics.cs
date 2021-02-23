using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task IngestMetric(Metric metric)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.Metrics (Timestamp, Action, Subject, MetricsKey) VALUES (@Timestamp, @Action, @Subject, @MetricsKey)", metric);
        }
        
        public async Task IngestAccess(string ip, string log)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.AccessLog (Timestamp, Action, Ip) VALUES (@Timestamp, @Action, @Ip)", new
            {
                Timestamp = DateTime.UtcNow,
                Ip = ip,
                Action = log
            });
        }
        
        public async Task<IEnumerable<AggregateMetric>> MetricsReport(string action)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<AggregateMetric>(@"
                select
                datefromparts(datepart(YEAR,Timestamp), datepart(MONTH,Timestamp), datepart(DAY,Timestamp)) as Date, 
                GroupingSubject as Subject,
                count(*) as Count 
                from dbo.metrics where 
                Action = @Action
                AND GroupingSubject in  (select DISTINCT GroupingSubject from dbo.Metrics
                 WHERE action = @Action
                 AND MetricsKey is not null 
                AND Subject != 'Default'
                AND Subject != 'untitled'
                AND TRY_CONVERT(uniqueidentifier, Subject) is null
                 AND Timestamp >= DATEADD(DAY, -1, GETUTCDATE()))
                group by
                 datefromparts(datepart(YEAR,Timestamp), datepart(MONTH,Timestamp), datepart(DAY,Timestamp)),
                 GroupingSubject
                 Order by  datefromparts(datepart(YEAR,Timestamp), datepart(MONTH,Timestamp), datepart(DAY,Timestamp)) asc", 
                    new {Action = action}))
                .ToList();
        }

        public async Task<List<(DateTime, string, string)>> FullTarReport(string key)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<(DateTime, string, string)>(@"
                                SELECT u.Timestamp, u.Path, u.MetricsKey FROM
                                (SELECT al.Timestamp, JSON_VALUE(al.Action, '$.Path') as Path, al.MetricsKey FROM dbo.AccessLog al
                                WHERE al.MetricsKey = @MetricsKey
                                UNION ALL
                                SELECT m.Timestamp, m.Action + ' ' + m.Subject as Path, m.MetricsKey FROM dbo.Metrics m
                                WHERE m.MetricsKey = @MetricsKey
                                AND m.Action != 'TarKey') u
                                ORDER BY u.Timestamp Desc",
                new {MetricsKey = key})).ToList();

        }

        public async Task<bool> ValidMetricsKey(string metricsKey)
        {
            await using var conn = await Open();
            return (await conn.QuerySingleOrDefaultAsync<string>("SELECT TOP(1) MetricsKey from dbo.MetricsKeys Where MetricsKey = @MetricsKey",
                new {MetricsKey = metricsKey})) != default;
        }

        public async Task AddMetricsKey(string metricsKey)
        {
            await using var conn = await Open();
            await using var trans = conn.BeginTransaction();

            if ((await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT TOP(1) MetricsKey from dbo.MetricsKeys Where MetricsKey = @MetricsKey",
                new {MetricsKey = metricsKey}, trans)) != default)
                return;
            
            await conn.ExecuteAsync("INSERT INTO dbo.MetricsKeys (MetricsKey) VALUES (@MetricsKey)",
                new {MetricsKey = metricsKey}, trans);
        }

        public async Task<string[]> AllKeys()
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<string>("SELECT MetricsKey from dbo.MetricsKeys")).ToArray();
        }


        public async Task<long> UniqueInstalls(string machineUrl)
        {
            await using var conn = await Open();
            return await conn.QueryFirstAsync<long>(
                @"SELECT COUNT(*) FROM (
                        SELECT DISTINCT MetricsKey from dbo.Metrics where Action = 'finish_install' and GroupingSubject in (
                        SELECT JSON_VALUE(Metadata, '$.title') FROM dbo.ModLists
                        WHERE JSON_VALUE(Metadata, '$.links.machineURL') = @MachineURL)) s",
                new {MachineURL = machineUrl});
        }
        
        public async Task<long> TotalInstalls(string machineUrl)
        {
            await using var conn = await Open();
            return await conn.QueryFirstAsync<long>(
                @"SELECT COUNT(*) from dbo.Metrics where Action = 'finish_install' and GroupingSubject in (
                        SELECT JSON_VALUE(Metadata, '$.title') FROM dbo.ModLists
                        WHERE JSON_VALUE(Metadata, '$.links.machineURL') = @MachineURL)",
                new {MachineURL = machineUrl});
        }

        public async Task<IEnumerable<(string, long)>> GetTotalInstalls()
        {
            await using var conn = await Open();
            return await conn.QueryAsync<(string, long)>(
                @"SELECT GroupingSubject, Count(*) as Count
                        From dbo.Metrics
                        WHERE 

                        GroupingSubject in  (select DISTINCT GroupingSubject from dbo.Metrics
                            WHERE action = 'finish_install'
                            AND MetricsKey is not null)
	                        group by GroupingSubject
	                        order by Count(*) desc");
        }
        
        public async Task<IEnumerable<(string, long)>> GetTotalUniqueInstalls()
        {
            await using var conn = await Open();
            return await conn.QueryAsync<(string, long)>(
                @"Select GroupingSubject, Count(*) as Count
                        FROM
                        (select DISTINCT MetricsKey, GroupingSubject
                        From dbo.Metrics
                        WHERE 
                        GroupingSubject in  (select DISTINCT GroupingSubject from dbo.Metrics
                            WHERE action = 'finish_install'
                            AND MetricsKey is not null)) m
                        GROUP BY GroupingSubject
                        Order by Count(*) desc
                        ");
        }

        public async IAsyncEnumerable<MetricRow> MetricsDump()
        {
            var keys = new Dictionary<string, long>();
            
            await using var conn = await Open();
            foreach (var row in await conn.QueryAsync<(long, DateTime, string, string, string, string)>(@"select Id, Timestamp, Action, Subject, MetricsKey, GroupingSubject from dbo.metrics WHERE MetricsKey is not null"))
            {
                if (!keys.TryGetValue(row.Item5, out var keyid))
                {
                    keyid = keys.Count;
                    keys[row.Item5] = keyid;
                }

                yield return new MetricRow
                {
                    Id = row.Item1,
                    Timestamp = row.Item2,
                    Action = row.Item3,
                    Subject = row.Item4,
                    MetricsKey = keyid,
                    GroupingSubject = row.Item6
                };
            }
        }

        public class MetricRow
        {
            public long Id;
            public DateTime Timestamp;
            public string Action;
            public string Subject;
            public string GroupingSubject;
            public long MetricsKey;
        }
    }
}
