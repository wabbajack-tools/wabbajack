using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Dapper;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Server.DataLayer
{
    /// <summary>
    /// SQL routines that read/write cached information from the Nexus
    /// </summary>
    public partial class SqlService
    {
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
        
        public async Task<ModInfo> GetNexusModInfoString(Game game, long modId)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Data FROM dbo.NexusModInfos WHERE Game = @Game AND @ModId = ModId",
                new {Game = game.MetaData().NexusGameId, ModId = modId});
            return result == null ? null : JsonConvert.DeserializeObject<ModInfo>(result);
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
        
        public async Task AddNexusModFileSlow(Game game, long modId, long fileId, DateTime lastCheckedUtc)
        {
            await using var conn = await Open();

            await conn.ExecuteAsync(                
                @"MERGE dbo.NexusModFilesSlow AS Target
                      USING (SELECT @GameId GameId, @ModId ModId, @LastChecked LastChecked, @FileId FileId) AS Source
                      ON Target.GameId = Source.GameId AND Target.ModId = Source.ModId AND Target.FileId = Source.FileId
                      WHEN MATCHED THEN UPDATE SET Target.LastChecked = @LastChecked
                      WHEN NOT MATCHED THEN INSERT (GameId, ModId, LastChecked, FileId) VALUES (@GameId, @ModId, @LastChecked, @FileId);",
                new
                {
                    GameId = game.MetaData().NexusGameId,
                    ModId = modId,
                    FileId = fileId,
                    LastChecked = lastCheckedUtc,
                });
        }
        
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modId)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Data FROM dbo.NexusModFiles WHERE Game = @Game AND @ModId = ModId",
                new {Game = game.MetaData().NexusGameId, ModId = modId});
            return result == null ? null : JsonConvert.DeserializeObject<NexusApiClient.GetModFilesResponse>(result);
        }

        public async Task PurgeNexusCache(long modId)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("DELETE FROM dbo.NexusModFiles WHERE ModId = @ModId", new {ModId = modId});
            await conn.ExecuteAsync("DELETE FROM dbo.NexusModInfos WHERE ModId = @ModId", new {ModId = modId});
        }

        public async Task<Dictionary<(Game, long), HTMLInterface.PermissionValue>> GetNexusPermissions()
        {
            await using var conn = await Open();

            var results =
                await conn.QueryAsync<(int, long, int)>("SELECT NexusGameID, ModID, Permissions FROM NexusModPermissions");
            return results.ToDictionary(f => (GameRegistry.ByNexusID[f.Item1], f.Item2),
                f => (HTMLInterface.PermissionValue)f.Item3);
        }

        public async Task SetNexusPermissions(IEnumerable<(Game, long, HTMLInterface.PermissionValue)> permissions)
        {
            await using var conn = await Open();
            var tx = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync("DELETE FROM NexusModPermissions", transaction:tx);

            foreach (var (game, modId, perm) in permissions)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO NexusModPermissions (NexusGameID, ModID, Permissions) VALUES (@NexusGameID, @ModID, @Permissions)",
                    new {NexusGameID = game.MetaData().NexusGameId, ModID = modId, Permissions = (int)perm}, tx);
            }

            await tx.CommitAsync();

        }

        public async Task UpdateGameMetadata()
        {
            await using var conn = await Open();
            var existing = (await conn.QueryAsync<string>("SELECT WabbajackName FROM dbo.GameMetadata")).ToHashSet();

            var missing = GameRegistry.Games.Values.Where(g => !existing.Contains(g.Game.ToString())).ToList();
            foreach (var add in missing.Where(g => g.NexusGameId != 0))
            {
                await conn.ExecuteAsync(
                    "INSERT INTO dbo.GameMetaData (NexusGameID, WabbajackName) VALUES (@NexusGameId, WabbajackName)",
                    new {NexusGameId = add.NexusGameId, WabbajackName = add.ToString()});
            }
        }

        public async Task SetNexusPermission(Game game, long modId, HTMLInterface.PermissionValue perm)
        {
            await using var conn = await Open();
            var tx = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync("DELETE FROM NexusModPermissions WHERE GameID = @GameID AND ModID = @ModID", new
                {
                    GameID = game.MetaData().NexusGameId,
                    ModID = modId
                },
                transaction:tx);
            
            await conn.ExecuteAsync(
                "INSERT INTO NexusModPermissions (NexusGameID, ModID, Permissions) VALUES (@NexusGameID, @ModID, @Permissions)",
                new {NexusGameID = game.MetaData().NexusGameId, ModID = modId, Permissions = (int)perm}, tx);

            await tx.CommitAsync();
        }
    }
}
