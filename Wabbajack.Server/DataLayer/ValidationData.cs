using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<ValidationData> GetValidationData()
        {

            var archiveStatus = AllModListArchivesStatus();
            var modLists = AllModLists();
            var mirrors = GetAllMirroredHashes();
            var authoredFiles = AllAuthoredFiles();
            var nexusFiles = await AllNexusFiles();
            return new ValidationData
            {
                NexusFiles = nexusFiles.ToDictionary(nf => (nf.NexusGameId, nf.ModId, nf.FileId), nf => nf.category),
                ArchiveStatus = await archiveStatus,
                ModLists = await modLists,
                Mirrors = await mirrors,
                AllowedMirrors = new Lazy<Task<Dictionary<Hash, string>>>(async () => await GetAllowedMirrors()),
                AllAuthoredFiles = await authoredFiles,
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

        public async Task<HashSet<(long NexusGameId, long ModId, long FileId, string category)>> AllNexusFiles()
        {
            await using var conn = await Open();
            var results = await conn.QueryAsync<(long, long, long, string)>(@"SELECT Game, ModId, FileId, JSON_VALUE(Data, '$.category_name') FROM dbo.NexusModFile");
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
