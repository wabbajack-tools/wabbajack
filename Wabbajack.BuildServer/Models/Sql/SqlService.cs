using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Wabbajack.BuildServer.Model.Models.Results;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Model.Models
{
    public class SqlService
    {
        private IConfiguration _configuration;
        private AppSettings _settings;

        public SqlService(AppSettings settings)
        {
            _settings = settings;

        }

        private async Task<SqlConnection> Open()
        {
            var conn = new SqlConnection(_settings.SqlConnection);
            await conn.OpenAsync();
            return conn;
        }

        public async Task MergeVirtualFile(VirtualFile vfile)
        {
            var files = new List<IndexedFile>();
            var contents = new List<ArchiveContent>();
            
            IngestFile(vfile, files, contents);

            files = files.DistinctBy(f => f.Hash).ToList();
            contents = contents.DistinctBy(c => (c.Parent, c.Path)).ToList();

            await using var conn = await Open();
            await conn.ExecuteAsync("dbo.MergeIndexedFiles", new {Files = files.ToDataTable(), Contents = contents.ToDataTable()},
                commandType: CommandType.StoredProcedure);
        }
        
        private static void IngestFile(VirtualFile root, ICollection<IndexedFile> files, ICollection<ArchiveContent> contents)
        {
            var hash = BitConverter.ToInt64(root.Hash.FromBase64());
            files.Add(new IndexedFile
            {
                Hash = hash,
                Sha256 = root.ExtendedHashes.SHA256.FromHex(),
                Sha1 = root.ExtendedHashes.SHA1.FromHex(),
                Md5 = root.ExtendedHashes.MD5.FromHex(),
                Crc32 = BitConverter.ToInt32(root.ExtendedHashes.CRC.FromHex()),
                Size = root.Size
            });

            if (root.Children == null) return;

            foreach (var child in root.Children)
            {
                IngestFile(child, files, contents);

                var child_hash = BitConverter.ToInt64(child.Hash.FromBase64());
                contents.Add(new ArchiveContent
                {
                    Parent = hash,
                    Child = child_hash,
                    Path = child.Name
                });
            }

        }

        public async Task<bool> HaveIndexdFile(string hash)
        {
            await using var conn = await Open();
            var row = await conn.QueryAsync(@"SELECT * FROM IndexedFile WHERE Hash = @Hash",
                new {Hash = BitConverter.ToInt64(hash.FromBase64())});
            return row.Any();
        }
        
        
                    
        class ArchiveContentsResult
        {
            public long Parent { get; set; }
            public long Hash { get; set; }
            public long Size { get; set; }
            public string Path { get; set; }
        }

        
        /// <summary>
        /// Get the name, path, hash and size of the file with the provided hash, and all files perhaps
        /// contained inside this file. Note: files themselves do not have paths, so the top level result
        /// will have a null path
        /// </summary>
        /// <param name="hash">The xxHash64 of the file to look up</param>
        /// <returns></returns>
        public async Task<IndexedVirtualFile> AllArchiveContents(long hash)
        {
            await using var conn = await Open();
            var files = await conn.QueryAsync<ArchiveContentsResult>(@"
              SELECT 0 as Parent, i.Hash, i.Size, null as Path FROM IndexedFile i WHERE Hash = @Hash
              UNION ALL
              SELECT a.Parent, i.Hash, i.Size, a.Path FROM AllArchiveContent a 
              LEFT JOIN IndexedFile i ON i.Hash = a.Child
              WHERE TopParent = @Hash",
                new {Hash = hash});

            var grouped = files.GroupBy(f => f.Parent).ToDictionary(f => f.Key, f=> (IEnumerable<ArchiveContentsResult>)f);

            List<IndexedVirtualFile> Build(long parent)
            {
                if (grouped.TryGetValue(parent, out var children))
                {
                    return children.Select(f => new IndexedVirtualFile
                    {
                        Name = f.Path,
                        Hash = BitConverter.GetBytes(f.Hash).ToBase64(),
                        Size = f.Size,
                        Children = Build(f.Hash)
                    }).ToList();
                }
                return new List<IndexedVirtualFile>();
            }
            return Build(0).FirstOrDefault();
        }

        public async Task IngestAllMetrics(IEnumerable<Metric> allMetrics)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.Metrics (Timestamp, Action, Subject, MetricsKey) VALUES (@Timestamp, @Action, @Subject, @MetricsKey)", allMetrics);
        }
        public async Task IngestMetric(Metric metric)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.Metrics (Timestamp, Action, Subject, MetricsKey) VALUES (@Timestamp, @Action, @Subject, @MetricsKey)", metric);
        }

        public async Task<IEnumerable<AggregateMetric>> MetricsReport(string action)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<AggregateMetric>(@"
                        SELECT d.Date, d.GroupingSubject as Subject, Count(*) as Count FROM 
                        (select DISTINCT CONVERT(date, Timestamp) as Date, GroupingSubject, Action, MetricsKey from dbo.Metrics) m
                        RIGHT OUTER JOIN
                        (SELECT CONVERT(date, DATEADD(DAY, number + 1, dbo.MinMetricDate())) as Date, GroupingSubject, Action
                        FROM master..spt_values
                        CROSS JOIN (
                          SELECT DISTINCT GroupingSubject, Action FROM dbo.Metrics 
                          WHERE MetricsKey is not null 
                          AND Subject != 'Default'
                          AND TRY_CONVERT(uniqueidentifier, Subject) is null) as keys
                        WHERE type = 'P'
                        AND DATEADD(DAY, number+1, dbo.MinMetricDate()) < dbo.MaxMetricDate()) as d
                        ON m.Date = d.Date AND m.GroupingSubject = d.GroupingSubject AND m.Action = d.Action
                        WHERE d.Action = @action
                        group by d.Date, d.GroupingSubject, d.Action
                        ORDER BY d.Date, d.GroupingSubject, d.Action", new {Action = action}))
                .ToList();
        }

    }
}
