using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.DTOs.CDN;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer;

public partial class SqlService
{
    public async Task TouchAuthoredFile(FileDefinition definition, DateTime? date = null)
    {
        await using var conn = await Open();
        if (date == null)
            await conn.ExecuteAsync(
                "UPDATE AuthoredFiles SET LastTouched = GETUTCDATE() WHERE ServerAssignedUniqueId = @Uid",
                new {Uid = definition.ServerAssignedUniqueId});
        else
            await conn.ExecuteAsync(
                "UPDATE AuthoredFiles SET LastTouched = @Date WHERE ServerAssignedUniqueId = @Uid",
                new {Uid = definition.ServerAssignedUniqueId, Date = date});
    }

    public async Task<FileDefinition> CreateAuthoredFile(FileDefinition definition, string login)
    {
        definition.Author = login;
        var uid = Guid.NewGuid().ToString();
        await using var conn = await Open();
        definition.ServerAssignedUniqueId = uid;
        await conn.ExecuteAsync(
            "INSERT INTO dbo.AuthoredFiles (ServerAssignedUniqueId, LastTouched, CDNFileDefinition) VALUES (@Uid, GETUTCDATE(), @CdnFile)",
            new
            {
                Uid = uid,
                CdnFile = definition
            });
        return definition;
    }

    public async Task Finalize(FileDefinition definition)
    {
        await using var conn = await Open();
        await conn.ExecuteAsync(
            "UPDATE AuthoredFiles SET LastTouched = GETUTCDATE(), Finalized = GETUTCDATE() WHERE ServerAssignedUniqueId = @Uid",
            new
            {
                Uid = definition.ServerAssignedUniqueId
            });
    }

    public async Task<FileDefinition> GetCDNFileDefinition(string serverAssignedUniqueId)
    {
        await using var conn = await Open();
        return (await conn.QueryAsync<FileDefinition>(
            "SELECT CDNFileDefinition FROM dbo.AuthoredFiles WHERE ServerAssignedUniqueID = @Uid",
            new {Uid = serverAssignedUniqueId})).First();
    }

    public async Task DeleteFileDefinition(FileDefinition definition)
    {
        await using var conn = await Open();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.AuthoredFiles WHERE ServerAssignedUniqueID = @Uid",
            new {Uid = definition.ServerAssignedUniqueId});
    }

    public async Task<IEnumerable<AuthoredFilesSummary>> AllAuthoredFiles()
    {
        await using var conn = await Open();
        var results = await conn.QueryAsync<AuthoredFilesSummary>(
            "SELECT CONVERT(NVARCHAR(50), ServerAssignedUniqueId) as ServerAssignedUniqueId, Size, OriginalFileName, Author, LastTouched, Finalized, MungedName from dbo.AuthoredFilesSummaries ORDER BY LastTouched DESC");
        return results;
    }
}