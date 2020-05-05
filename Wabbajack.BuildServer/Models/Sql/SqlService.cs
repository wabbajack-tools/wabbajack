using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Wabbajack.BuildServer.Model.Models.Results;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Model.Models
{
    public class SqlService
    {
        private AppSettings _settings;

        public SqlService(AppSettings settings)
        {
            _settings = settings;

        }

        public async Task<SqlConnection> Open()
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

        public async Task<IEnumerable<(RelativePath, Hash)>> GameFiles(Game game, Version version)
        {
            await using var conn = await Open();
            var files = await conn.QueryAsync<(RelativePath, Hash)>(
                @"SELECT Path, Hash FROM dbo.GameFiles where Game = @Game AND GameVersion = @GameVersion",
                new {Game = game.ToString(), GameVersion = version});

            return files;

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
                        AND DATEADD(DAY, number+1, dbo.MinMetricDate()) <= dbo.MaxMetricDate()) as d
                        ON m.Date = d.Date AND m.GroupingSubject = d.GroupingSubject AND m.Action = d.Action
                        WHERE d.Action = @action
                        AND d.Date >= DATEADD(month, -1, GETUTCDATE())
                        group by d.Date, d.GroupingSubject, d.Action
                        ORDER BY d.Date, d.GroupingSubject, d.Action", new {Action = action}))
                .ToList();
        }
        
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
                @"INSERT INTO dbo.Jobs (Created, Priority, PrimaryKeyString, Payload, OnSuccess) VALUES (GETUTCDATE(), @Priority, @PrimaryKeyString, @Payload, @OnSuccess)",
                new {
                    job.Priority,
                    PrimaryKeyString = job.Payload.PrimaryKeyString,
                    Payload = job.Payload.ToJson(), 
                    OnSuccess = job.OnSuccess?.ToJson() ?? null});
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
                @"UPDATE dbo.Jobs SET Ended = GETUTCDATE(), Success = @Success, ResultContent = @ResultContent WHERE Id = @Id",
                new {
                    job.Id,
                    Success = job.Result.ResultType == JobResultType.Success,
                    ResultContent = job.Result
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
            var result = await conn.QueryAsync<(long, DateTime, DateTime, DateTime, AJobPayload, int)>(
                @"UPDATE jobs SET Started = GETUTCDATE(), RunBy = @RunBy 
                        WHERE ID in (SELECT TOP(1) ID FROM Jobs 
                                       WHERE Started is NULL
                                       AND PrimaryKeyString NOT IN (SELECT PrimaryKeyString from jobs WHERE Started IS NOT NULL and Ended IS NULL)
                                       ORDER BY Priority DESC, Created);
                      SELECT TOP(1) Id, Started, Ended, Created, Payload, Priority FROM jobs WHERE RunBy = @RunBy ORDER BY Started DESC",
                new {RunBy = Guid.NewGuid().ToString()});
            return result.Select(k =>
                new Job {
                    Id = k.Item1,
                    Started = k.Item2,
                    Ended = k.Item3,
                    Created = k.Item4,
                    Payload = k.Item5,
                    Priority = (Job.JobPriority)k.Item6
                }).FirstOrDefault();
    }
        
        
        public async Task<IEnumerable<Job>> GetRunningJobs()
        {
            await using var conn = await Open();
            var results =
                await conn.QueryAsync<(long, DateTime, DateTime, DateTime, AJobPayload, int)>("SELECT Id, Started, Ended, Created, Payload, Priority FROM dbo.Jobs WHERE Started IS NOT NULL AND Ended IS NULL ");
            return results.Select(k =>
                new Job {
                    Id = k.Item1,
                    Started = k.Item2,
                    Ended = k.Item3,
                    Created = k.Item4,
                    Payload = k.Item5,
                    Priority = (Job.JobPriority)k.Item6
                });
        }


        public async Task<IEnumerable<Job>> GetUnfinishedJobs()
        {
            await using var conn = await Open();
            var results =
                await conn.QueryAsync<(long, DateTime, DateTime, DateTime, AJobPayload, int)>("SELECT Id, Started, Ended, Created, Payload, Priority from dbo.Jobs WHERE Ended IS NULL ");
            return results.Select(k =>
                new Job {
                    Id = k.Item1,
                    Started = k.Item2,
                    Ended = k.Item3,
                    Created = k.Item4,
                    Payload = k.Item5,
                    Priority = (Job.JobPriority)k.Item6
                });
        }

        
        #endregion


        #region TypeMappers

        static SqlService()
        {
            SqlMapper.AddTypeHandler(new HashMapper());
            SqlMapper.AddTypeHandler(new RelativePathMapper());
            SqlMapper.AddTypeHandler(new JsonMapper<AbstractDownloadState>());
            SqlMapper.AddTypeHandler(new JsonMapper<AJobPayload>());
            SqlMapper.AddTypeHandler(new JsonMapper<JobResult>());
            SqlMapper.AddTypeHandler(new JsonMapper<Job>());
            SqlMapper.AddTypeHandler(new VersionMapper());
            SqlMapper.AddTypeHandler(new GameMapper());
        }

        public class JsonMapper<T> : SqlMapper.TypeHandler<T>
        {
            public override void SetValue(IDbDataParameter parameter, T value)
            {
                parameter.Value = value.ToJson();
            }

            public override T Parse(object value)
            {
                return ((string)value).FromJsonString<T>();
            }
        }
        
        public class RelativePathMapper : SqlMapper.TypeHandler<RelativePath>
        {
            public override void SetValue(IDbDataParameter parameter, RelativePath value)
            {
                parameter.Value = value.ToJson();
            }

            public override RelativePath Parse(object value)
            {
                return (RelativePath)(string)value;
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
        
        class VersionMapper : SqlMapper.TypeHandler<Version>
        {
            public override void SetValue(IDbDataParameter parameter, Version value)
            {
                parameter.Value = value.ToString();
            }

            public override Version Parse(object value)
            {
                return Version.Parse((string)value);
            }
        }
        
        class GameMapper : SqlMapper.TypeHandler<Game>
        {
            public override void SetValue(IDbDataParameter parameter, Game value)
            {
                parameter.Value = value.ToString();
            }

            public override Game Parse(object value)
            {
                return GameRegistry.GetByFuzzyName((string)value).Game;
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
                    uf.Name,
                    uf.Size,
                    UploadedBy = uf.Uploader,
                    Hash = (long)uf.Hash,
                    uf.UploadDate,
                    uf.CDNName
                });
        }
        
        
        public async Task<UploadedFile> UploadedFileById(Guid fileId)
        {
            await using var conn = await Open();
            return await conn.QueryFirstAsync<UploadedFile>("SELECT * FROM dbo.UploadedFiles WHERE Id = @Id", 
                new {Id = fileId.ToString()});

        }

        public async Task<IEnumerable<UploadedFile>> AllUploadedFilesForUser(string user)
        {
            await using var conn = await Open();
            return await conn.QueryAsync<UploadedFile>("SELECT * FROM dbo.UploadedFiles WHERE UploadedBy = @uploadedBy", 
                new {UploadedBy = user});
        }
        
        
        public async Task<IEnumerable<UploadedFile>> AllUploadedFiles()
        {
            await using var conn = await Open();
            return await conn.QueryAsync<UploadedFile>("SELECT Id, Name, Size, UploadedBy as Uploader, Hash, UploadDate, CDNName FROM dbo.UploadedFiles ORDER BY UploadDate DESC");
        }
        
        public async Task DeleteUploadedFile(Guid dupId)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("SELECT * FROM dbo.UploadedFiles WHERE Id = @id", 
                new
                {
                    Id = dupId.ToString()
                });
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
                    JsonState = state.ToJson()
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
            var results = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT PrimaryKey FROM dbo.DownloadStates WHERE PrimaryKey = @PrimaryKey",
                new {PrimaryKey = key});
            return results != null;
        }

        public async Task AddNexusFileInfo(Game game, long modId, long fileId, DateTime lastCheckedUtc, NexusFileInfo data)
        {
            await using var conn = await Open();

            await conn.ExecuteAsync("INSERT INTO dbo.NexusFileInfos (Game, ModId, FileId, LastChecked, Data) VALUES " +
                                    "(@Game, @ModId, @FileId, @LastChecked, @Data)",
                new
                {
                    Game = game.MetaData().NexusGameId,
                    ModId = modId,
                    FileId = fileId,
                    LastChecked = lastCheckedUtc,
                    Data = JsonConvert.SerializeObject(data)
                });
            
        }

        public async Task AddNexusModInfo(Game game, long modId, DateTime lastCheckedUtc, ModInfo data)
        {
            await using var conn = await Open();

            await conn.ExecuteAsync(
                @"MERGE dbo.NexusModInfos AS Target
                      USING (SELECT @Game Game, @ModId ModId, @LastChecked LastChecked, @Data Data) AS Source
                      ON Target.Game = Source.Game AND Target.ModId = Source.ModId
                      WHEN MATCHED THEN UPDATE SET Target.Data = @Data, Target.LastChecked = @LastChecked
                      WHEN NOT MATCHED THEN INSERT (Game, ModId, LastChecked, Data) VALUES (@Game, @ModId, @LastChecked, @Data);",
                new
                {
                    Game = game.MetaData().NexusGameId,
                    ModId = modId,
                    LastChecked = lastCheckedUtc,
                    Data = JsonConvert.SerializeObject(data)
                });
            
        }
        
        public async Task AddNexusModFiles(Game game, long modId, DateTime lastCheckedUtc, NexusApiClient.GetModFilesResponse data)
        {
            await using var conn = await Open();

            await conn.ExecuteAsync(                
                @"MERGE dbo.NexusModFiles AS Target
                      USING (SELECT @Game Game, @ModId ModId, @LastChecked LastChecked, @Data Data) AS Source
                      ON Target.Game = Source.Game AND Target.ModId = Source.ModId
                      WHEN MATCHED THEN UPDATE SET Target.Data = @Data, Target.LastChecked = @LastChecked
                      WHEN NOT MATCHED THEN INSERT (Game, ModId, LastChecked, Data) VALUES (@Game, @ModId, @LastChecked, @Data);",
                new
                {
                    Game = game.MetaData().NexusGameId,
                    ModId = modId,
                    LastChecked = lastCheckedUtc,
                    Data = JsonConvert.SerializeObject(data)
                });
            
        }

        public async Task<ModInfo> GetNexusModInfoString(Game game, long modId)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Data FROM dbo.NexusModInfos WHERE Game = @Game AND @ModId = ModId",
                new {Game = game.MetaData().NexusGameId, ModId = modId});
            return result == null ? null : JsonConvert.DeserializeObject<ModInfo>(result);
        }

        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modId)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Data FROM dbo.NexusModFiles WHERE Game = @Game AND @ModId = ModId",
                new {Game = game.MetaData().NexusGameId, ModId = modId});
            return result == null ? null : JsonConvert.DeserializeObject<NexusApiClient.GetModFilesResponse>(result);
        }

        #region ModLists
        public async Task<IEnumerable<ModListSummary>> GetModListSummaries()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<string>("SELECT Summary from dbo.ModLists");
            return results.Select(s => s.FromJsonString<ModListSummary>()).ToList();
        }
        
        public async Task<DetailedStatus> GetDetailedModlistStatus(string machineUrl)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>("SELECT DetailedStatus from dbo.ModLists WHERE MachineURL = @MachineURL",
                new
                {
                    machineUrl
                });
            return result.FromJsonString<DetailedStatus>();
        }
        public async Task<List<DetailedStatus>> GetDetailedModlistStatuses()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<string>("SELECT DetailedStatus from dbo.ModLists");
            return results.Select(s => s.FromJsonString<DetailedStatus>()).ToList();
        }

        

        #endregion

        #region Logins
        public async Task<string> AddLogin(string name)
        {
            var key = NewAPIKey();
            await using var conn = await Open();


            await conn.ExecuteAsync("INSERT INTO dbo.ApiKeys (Owner, ApiKey) VALUES (@Owner, @ApiKey)",
                new {Owner = name, ApiKey = key});
            return key;
        }
        
        public static string NewAPIKey()
        {
            var arr = new byte[128];
            new Random().NextBytes(arr);
            return arr.ToHex();
        }
        
        public async Task<string> LoginByAPIKey(string key)
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<string>(@"SELECT Owner as Id FROM dbo.ApiKeys WHERE ApiKey = @ApiKey",
                new {ApiKey = key});
            return result.FirstOrDefault();
        }

        public async Task<IEnumerable<(string Owner, string Key)>> GetAllUserKeys()
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<(string Owner, string Key)>("SELECT Owner, ApiKey FROM dbo.ApiKeys");
            return result;
        }

        
        #endregion

        #region Auto-healing routines

        public async Task<Archive> GetNexusStateByHash(Hash startingHash)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(@"SELECT JsonState FROM dbo.DownloadStates  
                                                               WHERE Hash = @hash AND PrimaryKey like 'NexusDownloader+State|%'",
                new {Hash = (long)startingHash});
            return result == null ? null : new Archive(result.FromJsonString<AbstractDownloadState>())
            {
                Hash = startingHash
            };
        }
        
        public async Task<Archive> GetStateByHash(Hash startingHash)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(string, long)>(@"SELECT JsonState, indexed.Size FROM dbo.DownloadStates state
                                                       LEFT JOIN dbo.IndexedFile indexed ON indexed.Hash = state.Hash
                                                       WHERE state.Hash = @hash",
                new {Hash = (long)startingHash});
            return result == default ? null : new Archive(result.Item1.FromJsonString<AbstractDownloadState>())
            {
                Hash = startingHash,
                Size = result.Item2
            };
        }
        
        public async Task<Archive> DownloadStateByPrimaryKey(string primaryKey)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(long Hash, string State)>(@"SELECT Hash, JsonState FROM dbo.DownloadStates WHERE PrimaryKey = @PrimaryKey",
                new {PrimaryKey = primaryKey});
            return result == default ? null : new Archive(result.State.FromJsonString<AbstractDownloadState>())
            {
                Hash = Hash.FromLong(result.Hash)
            };
        }
        

        #endregion

        
        /// <summary>
        /// Returns a hashset the only contains hashes from the input that do not exist in IndexedArchives
        /// </summary>
        /// <param name="searching"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<HashSet<Hash>> FilterByExistingIndexedArchives(HashSet<Hash> searching)
        {
            await using var conn = await Open();
            var found = await conn.QueryAsync<long>("SELECT Hash from dbo.IndexedFile WHERE Hash in @Hashes",
                new {Hashes = searching.Select(h => (long)h)});
            return searching.Except(found.Select(h => Hash.FromLong(h)).ToHashSet()).ToHashSet();
       }

        
        /// <summary>
        /// Returns a hashset the only contains primary keys from the input that do not exist in IndexedArchives
        /// </summary>
        /// <param name="searching"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<HashSet<string>> FilterByExistingPrimaryKeys(HashSet<string> pks)
        {
            await using var conn = await Open();
            var results = new List<string>();

            foreach (var partition in pks.Partition(512))
            {
                var found = await conn.QueryAsync<string>(
                    "SELECT Hash from dbo.DownloadStates WHERE PrimaryKey in @PrimaryKeys",
                    new {PrimaryKeys = partition.ToList()});
                results.AddRange(found);
            }

            return pks.Except(results.ToHashSet()).ToHashSet();
        }

        public async Task<long> DeleteNexusModInfosUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            await using var conn = await Open();
            var deleted = await conn.ExecuteScalarAsync<long>(
                @"DELETE FROM dbo.NexusModInfos WHERE Game = @Game AND ModID = @ModId AND LastChecked < @Date
                      SELECT @@ROWCOUNT AS Deleted",
                new {Game = game.MetaData().NexusGameId, ModId = modId, @Date = date});
            return deleted;
        }
        
        public async Task<long> DeleteNexusModFilesUpdatedBeforeDate(Game game, long modId, DateTime date)
        {
            await using var conn = await Open();
            var deleted = await conn.ExecuteScalarAsync<long>(
                @"DELETE FROM dbo.NexusModFiles WHERE Game = @Game AND ModID = @ModId AND LastChecked < @Date
                      SELECT @@ROWCOUNT AS Deleted",
                new {Game = game.MetaData().NexusGameId, ModId = modId, Date = date});
            return deleted;
        }

        public async Task UpdateModListStatus(ModListStatus dto)
        {

        }

        public async Task IngestModList(Hash hash, ModlistMetadata metadata, ModList modlist)
        {
            await using var conn = await Open();
            await using var tran = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync(@"DELETE FROM dbo.ModLists Where MachineUrl = @MachineUrl",
                new {MachineUrl = metadata.Links.MachineURL}, tran);

            await conn.ExecuteAsync(
                @"INSERT INTO dbo.ModLists (MachineUrl, Hash, Metadata, ModList) VALUES (@MachineUrl, @Hash, @Metadata, @ModList)",
                new
                {
                    MachineUrl = metadata.Links.MachineURL,
                    Hash = hash,
                    MetaData = metadata.ToJson(),
                    ModList = modlist.ToJson()
                }, tran);
            
            var entries = modlist.Archives.Select(a =>
                new
                {
                    MachineUrl = metadata.Links.MachineURL,
                    Hash = a.Hash,
                    Size = a.Size,
                    State = a.State.ToJson(),
                    PrimaryKeyString = a.State.PrimaryKeyString
                }).ToArray();

            await conn.ExecuteAsync(@"DELETE FROM dbo.ModListArchives WHERE MachineURL = @machineURL",
                new {MachineUrl = metadata.Links.MachineURL}, tran);
            
            foreach (var entry in entries)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO dbo.ModListArchives (MachineURL, Hash, Size, PrimaryKeyString, State) VALUES (@MachineURL, @Hash, @Size, @PrimaryKeyString, @State)",
                    entry, tran);
            }
            
            await tran.CommitAsync();
        }

        public async Task<bool> HaveIndexedModlist(string machineUrl, Hash hash)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT MachineURL from dbo.Modlists WHERE MachineURL = @MachineUrl AND Hash = @Hash",
                new {MachineUrl = machineUrl, Hash = hash});
            return result != null;
        }

        public async Task<List<Archive>> GetNonNexusModlistArchives()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<(Hash Hash, long Size, string State)>(
                @"SELECT Hash, Size, State FROM dbo.ModListArchives WHERE PrimaryKeyString NOT LIKE 'NexusDownloader+State|%'");
            return results.Select(r => new Archive (r.State.FromJsonString<AbstractDownloadState>()) 
            {
                Size = r.Size,
                Hash = r.Hash,
                
                }).ToList();}

        public async Task UpdateNonNexusModlistArchivesStatus(IEnumerable<(Archive Archive, bool IsValid)> results)
        {
            await using var conn = await Open();
            var trans = await conn.BeginTransactionAsync();
            await conn.ExecuteAsync("DELETE FROM dbo.ModlistArchiveStatus;", transaction:trans);
            
            foreach (var itm in results.DistinctBy(itm => (itm.Archive.Hash, itm.Archive.State.PrimaryKeyString)))
            {
                await conn.ExecuteAsync(
                    @"INSERT INTO dbo.ModlistArchiveStatus (PrimaryKeyStringHash, PrimaryKeyString, Hash, IsValid) 
               VALUES (HASHBYTES('SHA2_256', @PrimaryKeyString), @PrimaryKeyString, @Hash, @IsValid)", new
                    {
                        PrimaryKeyString = itm.Archive.State.PrimaryKeyString,
                        Hash = itm.Archive.Hash,
                        IsValid = itm.IsValid
                    }, trans);
            }

            await trans.CommitAsync();
        }

        public async Task<ValidationData> GetValidationData()
        {
            var nexusFiles = AllNexusFiles();
            var archiveStatus = AllModListArchivesStatus();
            var modLists = AllModLists();
            var archivePatches = AllArchivePatches();

            return new ValidationData
            {
                NexusFiles = await nexusFiles,
                ArchiveStatus = await archiveStatus,
                ModLists = await modLists,
                ArchivePatches = await archivePatches
            };
        }

        public async Task<Dictionary<(string PrimaryKeyString, Hash Hash), bool>> AllModListArchivesStatus()
        {
            await using var conn = await Open();
            var results =
                await conn.QueryAsync<(string, Hash, bool)>(
                    @"SELECT PrimaryKeyString, Hash, IsValid FROM dbo.ModListArchiveStatus");
            return results.ToDictionary(v => (v.Item1, v.Item2), v => v.Item3);
        }

        public async Task<HashSet<(long NexusGameId, long ModId, long FileId)>> AllNexusFiles()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<(long, long, long)>(@"SELECT Game, ModId, p.file_id
                                                      FROM [NexusModFiles] files
                                                      CROSS APPLY
                                                      OPENJSON(Data, '$.files') WITH (file_id bigint '$.file_id', category varchar(max) '$.category_name') p 
                                                      WHERE p.category is not null");
            return results.ToHashSet();
        }
        
        public async Task<List<(ModlistMetadata, ModList)>> AllModLists()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<(string, string)>(@"SELECT Metadata, ModList FROM dbo.ModLists");
            return results.Select(m => (m.Item1.FromJsonString<ModlistMetadata>(), m.Item2.FromJsonString<ModList>())).ToList();
        }

        public class ValidationData
        {
            public HashSet<(long Game, long ModId, long FileId)> NexusFiles { get; set; }
            public Dictionary<(string PrimaryKeyString, Hash Hash), bool> ArchiveStatus { get; set; }
            public List<(ModlistMetadata Metadata, ModList ModList)> ModLists { get; set; }
            public List<ArchivePatch> ArchivePatches { get; set; }
        }


        #region ArchivePatches

        public class ArchivePatch
        {
            public Hash SrcHash { get; set; }
            public AbstractDownloadState SrcState { get; set; }
            public Hash DestHash { get; set; }
            public AbstractDownloadState DestState { get; set; }
            
            public RelativePath DestDownload { get; set; }
            public RelativePath SrcDownload { get; set; }
            public Uri CDNPath { get; set; }
        }

        public async Task UpsertArchivePatch(ArchivePatch patch)
        {
            await using var conn = await Open();

            await using var trans = conn.BeginTransaction();
            await conn.ExecuteAsync(@"DELETE FROM dbo.ArchivePatches 
                  WHERE SrcHash = @SrcHash 
                    AND DestHash = @DestHash 
                    AND SrcPrimaryKeyStringHash = HASHBYTES('SHA2-256', @SrcPrimaryKeyString)
                    AND DestPrimaryKeyStringHash = HASHBYTES('SHA2-256', @DestPrimaryKeyString)",
                new
                {
                    SrcHash = patch.SrcHash,
                    DestHash = patch.DestHash,
                    SrcPrimaryKeyString = patch.SrcState.PrimaryKeyString,
                    DestPrimaryKeyString = patch.DestState.PrimaryKeyString
                }, trans);

            await conn.ExecuteAsync(@"INSERT INTO dbo.ArchivePatches 
                     (SrcHash, SrcPrimaryKeyString, SrcPrimaryKeyStringHash, SrcState,
                     DestHash, DestPrimaryKeyString, DestPrimaryKeyStringHash, DestState,
                      
                      SrcDownload, DestDownload, CDNPath)
                      VALUES (@SrcHash, @SrcPrimaryKeyString, HASHBYTES('SHA2-256', @SrcPrimaryKeyString), @SrcState,
                              @DestHash, @DestPrimaryKeyString, HASHBYTES('SHA2-256', @DestPrimaryKeyString), @DestState,
                              @SrcDownload, @DestDownload, @CDNPAth)",
            new
            {
                SrcHash = patch.SrcHash,
                DestHash = patch.DestHash,
                SrcPrimaryKeyString = patch.SrcState.PrimaryKeyString,
                DestPrimaryKeyString = patch.DestState.PrimaryKeyString,
                SrcState = patch.SrcState.ToJson(),
                DestState = patch.DestState.ToString(),
                DestDownload = patch.DestDownload,
                SrcDownload = patch.SrcDownload,
                CDNPath = patch.CDNPath
            }, trans);

            await trans.CommitAsync();
        }

        public async Task<List<ArchivePatch>> AllArchivePatches()
        {
            await using var conn = await Open();

            var results =
                await conn.QueryAsync<(Hash, AbstractDownloadState, Hash, AbstractDownloadState, RelativePath, RelativePath, Uri)>(
                    @"SELECT SrcHash, SrcState, DestHash, DestState, SrcDownload, DestDownload, CDNPath FROM dbo.ArchivePatches");
            return results.Select(a => new ArchivePatch
            {
                SrcHash = a.Item1,
                SrcState = a.Item2,
                DestHash = a.Item3,
                DestState = a.Item4,
                SrcDownload = a.Item5,
                DestDownload = a.Item6,
                CDNPath = a.Item7
            }).ToList();
        }
        

        #endregion

        public async Task<IEnumerable<Job>> GetAllJobs(TimeSpan from)
        {
            await using var conn = await Open();
            var results =
                await conn.QueryAsync<(long, DateTime, DateTime, DateTime, AJobPayload, int)>("SELECT Id, Started, Ended, Created, Payload, Priority from dbo.Jobs WHERE Created >= @FromTime ",
                    new {FromTime = DateTime.UtcNow - from});
            return results.Select(k =>
                new Job {
                    Id = k.Item1,
                    Started = k.Item2,
                    Ended = k.Item3,
                    Created = k.Item4,
                    Payload = k.Item5,
                    Priority = (Job.JobPriority)k.Item6
                });
        }
    }
}
