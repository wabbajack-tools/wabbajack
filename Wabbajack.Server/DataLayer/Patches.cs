using System;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        /// <summary>
        /// Adds a patch record
        /// </summary>
        /// <param name="patch"></param>
        /// <returns></returns>
        public async Task AddPatch(Patch patch)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("INSERT INTO dbo.Patches (SrcId, DestId) VALUES (@SrcId, @DestId)",
                new {SrcId = patch.Src.Id, DestId = patch.Dest.Id});
        }
        
        /// <summary>
        /// Adds a patch record
        /// </summary>
        /// <param name="patch"></param>
        /// <returns></returns>
        public async Task FinializePatch(Patch patch)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("UPDATE dbo.Patches SET PatchSize = @Size, PatchHash = @PatchHash, Finished = @Finished WHERE SrcId = @SrcId AND DestID = @DestId",
                new
                {
                    SrcId = patch.Src.Id, 
                    DestId = patch.Dest.Id,
                    PatchHash = patch.PatchHash,
                    PatchSize = patch.PatchSize,
                    Finshed = patch.Finished
                });
        }

        public async Task<Patch> FindPatch(Guid src, Guid dest)
        {
            await using var conn = await Open();
            var patch = await conn.QueryFirstOrDefaultAsync<(Hash, long, DateTime?)>(
                "SELECT PatchHash, PatchSize, Finished FROM dbo.Patches WHERE SrcId = @SrcId AND DestId = @DestId",
                new
                {
                    SrcId = src,
                    DestId = dest
                });
            if (patch == default)
                return default(Patch);

            return new Patch {
                Src = await GetArchiveDownload(src), 
                Dest = await GetArchiveDownload(dest),
                PatchHash = patch.Item1,
                PatchSize = patch.Item2,
                Finished = patch.Item3
            };

        }
    }
}
