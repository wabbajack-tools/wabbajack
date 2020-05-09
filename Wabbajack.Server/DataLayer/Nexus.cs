using System;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

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
        
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modId)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Data FROM dbo.NexusModFiles WHERE Game = @Game AND @ModId = ModId",
                new {Game = game.MetaData().NexusGameId, ModId = modId});
            return result == null ? null : JsonConvert.DeserializeObject<NexusApiClient.GetModFilesResponse>(result);
        }
    }
}
