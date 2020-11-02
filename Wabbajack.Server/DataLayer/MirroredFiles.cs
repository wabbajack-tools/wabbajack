using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<MirroredFile> GetNextMirroredFile()
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(Hash, DateTime, DateTime, string, string)>(
                "SELECT Hash, Created, Uploaded, Rationale, FailMessage from dbo.MirroredArchives WHERE Uploaded IS NULL");
            if (result == default) return null;
            return new MirroredFile
            {
                Hash = result.Item1, Created = result.Item2, Uploaded = result.Item3, Rationale = result.Item4, FailMessage = result.Item5
            };
        }
        
        public async Task<HashSet<Hash>> GetAllMirroredHashes()
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<Hash>("SELECT Hash FROM dbo.MirroredArchives")).ToHashSet();
        }
        
        public async Task UpsertMirroredFile(MirroredFile file)
        {
            await using var conn = await Open();
            await using var trans = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync("DELETE FROM dbo.MirroredArchives WHERE Hash = @Hash", new {file.Hash}, trans);
            await conn.ExecuteAsync(
                "INSERT INTO dbo.MirroredArchives (Hash, Created, Uploaded, Rationale, FailMessage) VALUES (@Hash, @Created, @Uploaded, @Rationale, @FailMessage)",
                new
                {
                    Hash = file.Hash,
                    Created = file.Created,
                    Uploaded = file.Uploaded,
                    Rationale = file.Rationale,
                    FailMessage = file.FailMessage
                }, trans);
            await trans.CommitAsync();
        }

        public async Task DeleteMirroredFile(Hash hash)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync("DELETE FROM dbo.MirroredArchives WHERE Hash = @Hash",
                new {Hash = hash});
        }

        public async Task InsertAllNexusMirrors()
        {
            var permissions = (await GetNexusPermissions()).Where(p => p.Value == HTMLInterface.PermissionValue.Yes);
            var downloads = (await GetAllArchiveDownloadStates()).Where(a => a.State is NexusDownloader.State).ToDictionary(a =>
            {
                var nd = (NexusDownloader.State)a.State;
                return (nd.Game, nd.ModID);
            }, a => a.Hash);
            
            var existing = await GetAllMirroredHashes();

            foreach (var (key, _) in permissions)
            {
                if (!downloads.TryGetValue(key, out var hash)) continue;
                if (existing.Contains(hash)) continue;

                await UpsertMirroredFile(new MirroredFile
                {
                    Hash = hash,
                    Created = DateTime.UtcNow,
                    Rationale =
                        $"Mod ({key.Item1} {key.Item2}) has allowed re-upload permissions on the Nexus"
                });
            }
        }

        public async Task<bool> HaveMirror(Hash hash)
        {
            await using var conn = await Open();

            return await conn.QueryFirstOrDefaultAsync<Hash>("SELECT Hash FROM dbo.MirroredArchives WHERE Hash = @Hash",
                new {Hash = hash}) != default;
        }

        public async Task QueueMirroredFiles()
        {
            await using var conn = await Open();

            await conn.ExecuteAsync(@"

                INSERT INTO dbo.MirroredArchives (Hash, Created, Rationale)

                SELECT hs.Hash, GETUTCDATE(), 'File has re-upload permissions on the Nexus' FROM
                (SELECT DISTINCT ad.Hash FROM dbo.NexusModPermissions p
                INNER JOIN GameMetadata md on md.NexusGameId = p.NexusGameID
                INNER JOIN dbo.ArchiveDownloads ad on ad.PrimaryKeyString like 'NexusDownloader+State|'+md.WabbajackName+'|'+CAST(p.ModID as nvarchar)+'|%'
                WHERE p.Permissions = 1
                AND ad.Hash not in (SELECT Hash from dbo.MirroredArchives)
                ) hs

                INSERT INTO dbo.MirroredArchives (Hash, Created, Rationale)
                SELECT DISTINCT Hash, GETUTCDATE(), 'File is hosted on GitHub'
                FROM dbo.ArchiveDownloads ad WHERE PrimaryKeyString like '%github.com/%'
                AND ad.Hash not in (SELECT Hash from dbo.MirroredArchives)


                INSERT INTO dbo.MirroredArchives (Hash, Created, Rationale)
                SELECT DISTINCT Hash, GETUTCDATE(), 'File license allows uploading to any Non-nexus site'
                FROM dbo.ArchiveDownloads ad WHERE PrimaryKeyString like '%enbdev.com/%'
                AND ad.Hash not in (SELECT Hash from dbo.MirroredArchives)

                INSERT INTO dbo.MirroredArchives (Hash, Created, Rationale)
                SELECT DISTINCT Hash, GETUTCDATE(), 'DynDOLOD file' /*, Name*/
                from dbo.ModListArchives mla WHERE Name like '%DynDoLOD%standalone%'
                and Hash not in (select Hash from dbo.MirroredArchives)

                INSERT INTO dbo.MirroredArchives (Hash, Created, Rationale)
                SELECT DISTINCT Hash, GETUTCDATE(), 'Distribution allowed by author' /*, Name*/
                from dbo.ModListArchives mla WHERE Name like '%particle%patch%'
                and Hash not in (select Hash from dbo.MirroredArchives)


                ");
        }
    }
}
