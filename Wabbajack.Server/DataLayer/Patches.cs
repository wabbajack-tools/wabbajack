using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;
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
            await conn.ExecuteAsync("UPDATE dbo.Patches SET PatchSize = @PatchSize, Finished = @Finished, IsFailed = @IsFailed, FailMessage = @FailMessage WHERE SrcId = @SrcId AND DestID = @DestId",
                new
                {
                    SrcId = patch.Src.Id, 
                    DestId = patch.Dest.Id,
                    PatchSize = patch.PatchSize,
                    Finished = patch.Finished,
                    IsFailed = patch.IsFailed,
                    FailMessage = patch.FailMessage
                });
        }

        public async Task<Patch> FindPatch(Guid src, Guid dest)
        {
            await using var conn = await Open();
            var patch = await conn.QueryFirstOrDefaultAsync<(long, DateTime?, bool?, string)>(
                @"SELECT p.PatchHash, p.PatchSize, p.Finished, p.IsFailed, p.FailMessage 
                      FROM dbo.Patches p
                      LEFT JOIN dbo.ArchiveDownloads src ON p.SrcId = src.Id
                      LEFT JOIN dbo.ArchiveDownloads dest ON p.SrcId = dest.Id
                      WHERE SrcId = @SrcId 
                        AND DestId = @DestId
                        AND src.DownloadFinished IS NOT NULL
                        AND dest.DownloadFinished IS NOT NULL",
                new
                {
                    SrcId = src,
                    DestId = dest
                });
            if (patch == default)
                return default;

            return new Patch {
                Src = await GetArchiveDownload(src), 
                Dest = await GetArchiveDownload(dest),
                PatchSize = patch.Item1,
                Finished = patch.Item2,
                IsFailed = patch.Item3,
                FailMessage = patch.Item4
            };
        }
        
        public async Task<Patch> FindOrEnqueuePatch(Guid src, Guid dest)
        {
            await using var conn = await Open();
            var trans = await conn.BeginTransactionAsync();
            var patch = await conn.QueryFirstOrDefaultAsync<(Guid, Guid, long, DateTime?, bool?, string)>(
                "SELECT SrcId, DestId, PatchSize, Finished, IsFailed, FailMessage FROM dbo.Patches WHERE SrcId = @SrcId AND DestId = @DestId",
                new
                {
                    SrcId = src,
                    DestId = dest
                }, trans);
            if (patch == default)
            {
                await conn.ExecuteAsync("INSERT INTO dbo.Patches (SrcId, DestId) VALUES (@SrcId, @DestId)",
                    new {SrcId = src, DestId = dest}, trans);
                await trans.CommitAsync();
                return new Patch {Src = await GetArchiveDownload(src), Dest = await GetArchiveDownload(dest),};
            }
            else
            {
                return new Patch {
                    Src = await GetArchiveDownload(src), 
                    Dest = await GetArchiveDownload(dest),
                    PatchSize = patch.Item3,
                    Finished = patch.Item4,
                    IsFailed = patch.Item5,
                    FailMessage = patch.Item6
                };
                
            }

        }

        public async Task<Patch> GetPendingPatch()
        {
            await using var conn = await Open();
            var patch = await conn.QueryFirstOrDefaultAsync<(Guid, Guid, long, DateTime?, bool?, string)>(
                @"SELECT p.SrcId, p.DestId, p.PatchSize, p.Finished, p.IsFailed, p.FailMessage FROM dbo.Patches p 
                      LEFT JOIN dbo.ArchiveDownloads src ON src.Id = p.SrcId
                      LEFT JOIN dbo.ArchiveDownloads dest ON dest.Id = p.DestId
                      WHERE p.Finished is NULL AND src.IsFailed = 0 AND dest.IsFailed = 0 ");
            if (patch == default)
                return default(Patch);

            return new Patch {
                Src = await GetArchiveDownload(patch.Item1), 
                Dest = await GetArchiveDownload(patch.Item2),
                PatchSize = patch.Item3,
                Finished = patch.Item4,
                IsFailed = patch.Item5,
                FailMessage = patch.Item6
            };
        }

        public async Task<List<Patch>> PatchesForSource(Guid sourceDownload)
        {
            await using var conn = await Open();
            var patches = await conn.QueryAsync<(Guid, Guid, long, DateTime?, bool?, string)>(
                "SELECT SrcId, DestId, PatchSize, Finished, IsFailed, FailMessage FROM dbo.Patches WHERE SrcId = @SrcId", new {SrcId = sourceDownload});

            List<Patch> results = new List<Patch>();
            foreach (var (srcId, destId, patchSize, finished, isFailed, failMessage) in patches)
            {
                results.Add( new Patch {
                    Src = await GetArchiveDownload(srcId), 
                    Dest = await GetArchiveDownload(destId),
                    PatchSize = patchSize,
                    Finished = finished,
                    IsFailed = isFailed,
                    FailMessage = failMessage
                });
            } 
            return results;
        }

        public async Task MarkPatchUsage(Guid srcId, Guid destId)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(
                @"UPDATE dbo.Patches SET Downloads = Downloads + 1, LastUsed = GETUTCDATE() WHERE SrcId = @srcId AND DestID = @destId",
                new {SrcId = srcId, DestId = destId});

        }
    }
}
