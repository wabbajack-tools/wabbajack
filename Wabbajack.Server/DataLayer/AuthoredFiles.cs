using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task TouchAuthoredFile(CDNFileDefinition definition)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("UPDATE AuthoredFiles SET LastTouched = GETUTCDATE() WHERE ServerAssignedUniqueId = @Uid",
                new {
                    Uid = definition.ServerAssignedUniqueId
                });
        }

        public async Task<CDNFileDefinition> CreateAuthoredFile(CDNFileDefinition definition, string login)
        {
            definition.Author = login;
            var uid = Guid.NewGuid().ToString();
            await using var conn = await Open();
            definition.ServerAssignedUniqueId = uid;
            await conn.ExecuteAsync("INSERT INTO dbo.AuthoredFiles (ServerAssignedUniqueId, LastTouched, CDNFileDefinition) VALUES (@Uid, GETUTCDATE(), @CdnFile)",
                new {
                    Uid = uid,
                    CdnFile = definition
                });
            return definition;
        }

        public async Task Finalize(CDNFileDefinition definition)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("UPDATE AuthoredFiles SET LastTouched = GETUTCDATE(), Finalized = GETUTCDATE() WHERE ServerAssignedUniqueId = @Uid",
                new {
                    Uid = definition.ServerAssignedUniqueId
                });
        }

        public async Task<CDNFileDefinition> GetCDNFileDefinition(string serverAssignedUniqueId)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<CDNFileDefinition>(
                "SELECT CDNFileDefinition FROM dbo.AuthoredFiles WHERE ServerAssignedUniqueID = @Uid",
                new {Uid = serverAssignedUniqueId})).First();
        }
        
        public async Task<CDNFileDefinition> DeleteFileDefinition(CDNFileDefinition definition)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<CDNFileDefinition>(
                "DELETE FROM dbo.AuthoredFiles WHERE ServerAssignedUniqueID = @Uid",
                new {Uid = definition.ServerAssignedUniqueId})).First();
        }

        
    }
}
