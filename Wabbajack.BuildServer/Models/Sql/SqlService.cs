using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using ReactiveUI;
using Wabbajack.BuildServer.Model.Models.Results;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
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
            files.Add(new IndexedFile
            {
                Hash = (long)root.Hash,
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

                contents.Add(new ArchiveContent
                {
                    Parent = (long)root.Hash,
                    Child = (long)child.Hash,
                    Path = (RelativePath)child.Name
                });
            }

        }

        public async Task<bool> HaveIndexdFile(Hash hash)
        {
            await using var conn = await Open();
            var row = await conn.QueryAsync(@"SELECT * FROM IndexedFile WHERE Hash = @Hash",
                new {Hash = (long)hash});
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
                        Name = (RelativePath)f.Path,
                        Hash = Hash.FromLong(f.Hash),
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
        
        #region UserRoutines

        public async Task<string> LoginByAPIKey(string key)
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<string>(@"SELECT Owner as Id FROM dbo.ApiKeys WHERE ApiKey = @Key",
                new {Key = key});
            return result.FirstOrDefault();
        }
        
        #endregion
        
        #region JobRoutines

        /// <summary>
        /// Enqueue a Job into the Job queue to be run at a later time
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public async Task EnqueueJob(Job job)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(
                @"INSERT INTO dbo.Jobs (Created, Priority, Payload, OnSuccess) VALUES (GETDATE(), @Priority, @Payload, @OnSuccess)",
                new {
                    Priority = job.Priority,
                    Payload = job.Payload.ToJSON(), 
                    OnSuccess = job.OnSuccess?.ToJSON() ?? null});
        }
        
        /// <summary>
        /// Enqueue a Job into the Job queue to be run at a later time
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public async Task FinishJob(Job job)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(
                @"UPDATE dbo.Jobs SET Finshed = GETDATE(), Success = @Success, ResultContent = @ResultContent WHERE Id = @Id",
                new { 
                    Id = job.Id,
                    Success = job.Result.ResultType == JobResultType.Success,
                    ResultPayload = job.Result.ToJSON()
                    
                });
            
            if (job.OnSuccess != null)
                await EnqueueJob(job.OnSuccess);
        }

        
        /// <summary>
        /// Get a Job from the Job queue to run. 
        /// </summary>
        /// <returns></returns>
        public async Task<Job> GetJob()
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<Job>(
                @"UPDATE jobs SET Started = GETDATE(), RunBy = @RunBy WHERE ID in (SELECT TOP(1) ID FROM Jobs WHERE Started is NULL ORDER BY Priority DESC, Created);
                      SELECT TOP(1) * FROM jobs WHERE RunBy = @RunBy ORDER BY Started DESC",
                new {RunBy = Guid.NewGuid().ToString()});
            return result.FirstOrDefault();
        }

        
        #endregion


        #region TypeMappers

        static SqlService()
        {
            SqlMapper.AddTypeHandler(new PayloadMapper());
            SqlMapper.AddTypeHandler(new HashMapper());
        }

        public class PayloadMapper : SqlMapper.TypeHandler<AJobPayload>
        {
            public override void SetValue(IDbDataParameter parameter, AJobPayload value)
            {
                parameter.Value = value.ToJSON();
            }

            public override AJobPayload Parse(object value)
            {
                return Utils.FromJSONString<AJobPayload>((string)value);
            }
        }
        
        class HashMapper : SqlMapper.TypeHandler<Hash>
        {
            public override void SetValue(IDbDataParameter parameter, Hash value)
            {
                parameter.Value = (long)value;
            }

            public override Hash Parse(object value)
            {
                return Hash.FromLong((long)value);
            }
        }
        

        #endregion

        public async Task AddUploadedFile(UploadedFile uf)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(
                "INSERT INTO dbo.UploadedFiles (Id, Name, Size, UploadedBy, Hash, UploadDate, CDNName) VALUES " +
                "(@Id, @Name, @Size, @UploadedBy, @Hash, @UploadDate, @CDNName)",
                new
                {
                    Id = uf.Id.ToString(),
                    Name = uf.Name,
                    Size = uf.Size,
                    UploadedBy = uf.Uploader,
                    Hash = (long)uf.Hash,
                    UploadDate = uf.UploadDate,
                    CDNName = uf.CDNName
                });
        }

        public async Task<IEnumerable<UploadedFile>> AllUploadedFilesForUser(string user)
        {
            await using var conn = await Open();
            return await conn.QueryAsync<UploadedFile>("SELECT * FROM dbo.UploadedFiles WHERE UploadedBy = @uploadedBy", 
                new {UploadedBy = user});
        }

        public async Task AddDownloadState(Hash hash, AbstractDownloadState state)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("INSERT INTO dbo.DownloadStates (Id, Hash, PrimaryKey, IniState, JsonState) " +
                                    "VALUES (@Id, @Hash, @PrimaryKey, @IniState, @JsonState)",
                new
                {
                    Id = state.PrimaryKeyString.StringSha256Hex().FromHex(),
                    Hash = hash,
                    PrimaryKey = state.PrimaryKeyString,
                    IniState = string.Join("\n", state.GetMetaIni()),
                    JsonState = state.ToJSON()
                });
        }

        public async Task<string> GetIniForHash(Hash id)
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<string>("SELECT IniState FROM dbo.DownloadStates WHERE Hash = @Hash",
            new {
                Hash = id
            });

            return results.FirstOrDefault();

        }

        public async Task<bool> HaveIndexedArchivePrimaryKey(string key)
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<string>(
                "SELECT * FROM dbo.DownloadStates WHERE PrimaryKey = @PrimaryKey",
                new {PrimaryKey = key});
            return results.Any();
        }
    }
}
