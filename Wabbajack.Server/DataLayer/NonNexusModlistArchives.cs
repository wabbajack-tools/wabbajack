using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        
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
    }
}
