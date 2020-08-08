using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<ValidationData> GetValidationData()
        {
            var nexusFiles = AllNexusFiles();
            var archiveStatus = AllModListArchivesStatus();
            var modLists = AllModLists();
            var mirrors = GetAllMirroredHashes();
            return new ValidationData
            {
                NexusFiles = new ConcurrentHashSet<(long Game, long ModId, long FileId)>((await nexusFiles).Select(f => (f.NexusGameId, f.ModId, f.FileId))),
                ArchiveStatus = await archiveStatus,
                ModLists = await modLists,
                Mirrors = await mirrors,
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

        public async Task<HashSet<(long NexusGameId, long ModId, long FileId, DateTime LastChecked)>> AllNexusFiles()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<(long, long, long, DateTime)>(@"SELECT Game, ModId, p.file_id, LastChecked
          FROM [NexusModFiles] files
          CROSS APPLY
          OPENJSON(Data, '$.files') WITH (file_id bigint '$.file_id', category varchar(max) '$.category_name') p 
          WHERE p.category is not null
          UNION 
          SELECT GameId, ModId, FileId, LastChecked FROM dbo.NexusModFilesSlow
          ");
            return results.ToHashSet();
        }
        
        public async Task<List<ModlistMetadata>> AllModLists()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<ModlistMetadata>(@"SELECT Metadata FROM dbo.ModLists");
            return results.ToList();
        }
    }
}
