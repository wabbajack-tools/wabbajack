using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi.DTOs;

namespace Wabbajack.Server.DataLayer;

/// <summary>
///     SQL routines that read/write cached information from the Nexus
/// </summary>
public partial class SqlService
{
    public async Task<long> DeleteNexusModInfosUpdatedBeforeDate(Game game, long modId, DateTime date)
    {
        await using var conn = await Open();
        var deleted = await conn.ExecuteScalarAsync<long>(
            @"DELETE FROM dbo.NexusModInfos WHERE Game = @Game AND ModID = @ModId AND LastChecked < @Date
                      SELECT @@ROWCOUNT AS Deleted",
            new {Game = game.MetaData().NexusGameId, ModId = modId, Date = date});
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

    public async Task<ModInfo?> GetNexusModInfoString(Game game, long modId)
    {
        await using var conn = await Open();
        var result = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Data FROM dbo.NexusModInfos WHERE Game = @Game AND @ModId = ModId",
            new {Game = game.MetaData().NexusGameId, ModId = modId});
        return result == null ? null : _dtos.Deserialize<ModInfo>(result);
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
                Data = _dtos.Serialize(data)
            });
    }

    public async Task AddNexusModFiles(Game game, long modId, DateTime lastCheckedUtc, ModFiles data)
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
                Data = _dtos.Serialize(data)
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
                LastChecked = lastCheckedUtc
            });
    }

    public async Task<ModFiles?> GetModFiles(Game game, long modId)
    {
        await using var conn = await Open();
        var result = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Data FROM dbo.NexusModFiles WHERE Game = @Game AND @ModId = ModId",
            new {Game = game.MetaData().NexusGameId, ModId = modId});
        return result == null ? null : _dtos.Deserialize<ModFiles>(result);
    }

    public async Task PurgeNexusCache(long modId)
    {
        await using var conn = await Open();
        await conn.ExecuteAsync("DELETE FROM dbo.NexusModFiles WHERE ModId = @ModId", new {ModId = modId});
        await conn.ExecuteAsync("DELETE FROM dbo.NexusModInfos WHERE ModId = @ModId", new {ModId = modId});
        await conn.ExecuteAsync("DELETE FROM dbo.NexusModPermissions WHERE ModId = @ModId", new {ModId = modId});
        await conn.ExecuteAsync("DELETE FROM dbo.NexusModFile WHERE ModId = @ModID", new {ModId = modId});
    }

    public async Task UpdateGameMetadata()
    {
        await using var conn = await Open();
        var existing = (await conn.QueryAsync<string>("SELECT WabbajackName FROM dbo.GameMetadata")).ToHashSet();

        var missing = GameRegistry.Games.Values.Where(g => !existing.Contains(g.Game.ToString())).ToList();
        foreach (var add in missing.Where(g => g.NexusGameId != 0))
            await conn.ExecuteAsync(
                "INSERT INTO dbo.GameMetaData (NexusGameID, WabbajackName) VALUES (@NexusGameId, @WabbajackName)",
                new {add.NexusGameId, WabbajackName = add.Game.ToString()});
    }

    public async Task<ModFile?> GetModFile(Game game, long modId, long fileId)
    {
        await using var conn = await Open();
        var result = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Data FROM dbo.NexusModFile WHERE Game = @Game AND @ModId = ModId AND @FileId = FileId",
            new {Game = game.MetaData().NexusGameId, ModId = modId, FileId = fileId});
        return result == null ? null : _dtos.Deserialize<ModFile>(result);
    }

    public async Task AddNexusModFile(Game game, long modId, long fileId, DateTime lastCheckedUtc, ModFile data)
    {
        await using var conn = await Open();

        await conn.ExecuteAsync(
            @"INSERT INTO dbo.NexusModFile (Game, ModId, FileId, LastChecked, Data)
                     VALUES (@Game, @ModId, @FileId, @LastChecked, @Data)",
            new
            {
                Game = game.MetaData().NexusGameId,
                ModId = modId,
                FileId = fileId,
                LastChecked = lastCheckedUtc,
                Data = _dtos.Serialize(data)
            });
    }
}