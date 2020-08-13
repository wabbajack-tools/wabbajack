using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<Guid> AddKnownDownload(Archive a, DateTime downloadFinished)
        {
            await using var conn = await Open();
            var Id = Guid.NewGuid();
            await conn.ExecuteAsync(
                "INSERT INTO ArchiveDownloads (Id, PrimaryKeyString, Size, Hash, DownloadState, Downloader, DownloadFinished, IsFailed) VALUES (@Id, @PrimaryKeyString, @Size, @Hash, @DownloadState, @Downloader, @DownloadFinished, @IsFailed)",
                new
                {
                    Id = Id,
                    PrimaryKeyString = a.State.PrimaryKeyString,
                    Size = a.Size == 0 ? null : (long?)a.Size,
                    Hash = a.Hash == default ? null : (Hash?)a.Hash,
                    DownloadState = a.State,
                    Downloader = AbstractDownloadState.TypeToName[a.State.GetType()],
                    DownloadFinished = downloadFinished,
                    IsFailed = false
                });
            return Id;
        }
        
        public async Task<Guid> EnqueueDownload(Archive a)
        {
            await using var conn = await Open();
            var Id = Guid.NewGuid();
            await conn.ExecuteAsync(
                "INSERT INTO ArchiveDownloads (Id, PrimaryKeyString, Size, Hash, DownloadState, Downloader) VALUES (@Id, @PrimaryKeyString, @Size, @Hash, @DownloadState, @Downloader)",
                new
                {
                    Id = Id,
                    PrimaryKeyString = a.State.PrimaryKeyString,
                    Size = a.Size == 0 ? null : (long?)a.Size,
                    Hash = a.Hash == default ? null : (Hash?)a.Hash,
                    DownloadState = a.State,
                    Downloader = AbstractDownloadState.TypeToName[a.State.GetType()],
                });
            return Id;
        }

        public async Task<HashSet<(Hash Hash, string PrimaryKeyString)>> GetAllArchiveDownloads()
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<(Hash, string)>("SELECT Hash, PrimaryKeyString FROM ArchiveDownloads")).ToHashSet();
        }
        
        public async Task<HashSet<(Hash Hash, AbstractDownloadState State)>> GetAllArchiveDownloadStates()
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<(Hash, AbstractDownloadState)>("SELECT Hash, DownloadState FROM ArchiveDownloads")).ToHashSet();
        }

        
        public async Task<ArchiveDownload> GetArchiveDownload(Guid id)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, bool?, AbstractDownloadState, DateTime?)>(
                "SELECT Id, Size, Hash, IsFailed, DownloadState, DownloadFinished FROM dbo.ArchiveDownloads WHERE Id = @id",
                new {Id = id});
            if (result == default)
                return null;

            return new ArchiveDownload
            {
                Id = result.Item1,
                IsFailed = result.Item4,
                DownloadFinished = result.Item6,
                Archive = new Archive(result.Item5) {Size = result.Item2 ?? 0, Hash = result.Item3 ?? default}
            };

        }
        
        public async Task<ArchiveDownload> GetArchiveDownload(string primaryKeyString)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, bool?, AbstractDownloadState, DateTime?)>(
                "SELECT Id, Size, Hash, IsFailed, DownloadState, DownloadFinished FROM dbo.ArchiveDownloads WHERE PrimaryKeyString = @PrimaryKeyString AND IsFailed = 0",
                new {PrimaryKeyString = primaryKeyString});
            if (result == default)
                return null;

            return new ArchiveDownload
            {
                Id = result.Item1,
                IsFailed = result.Item4,
                DownloadFinished = result.Item6,
                Archive = new Archive(result.Item5) {Size = result.Item2 ?? 0, Hash = result.Item3 ?? default}
            };

        }
        
        public async Task<ArchiveDownload> GetArchiveDownload(string primaryKeyString, Hash hash, long size)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, bool?, AbstractDownloadState, DateTime?)>(
                "SELECT Id, Size, Hash, IsFailed, DownloadState, DownloadFinished FROM dbo.ArchiveDownloads WHERE PrimaryKeyString = @PrimaryKeyString AND Hash = @Hash AND Size = @Size",
                new
                {
                    PrimaryKeyString = primaryKeyString,
                    Hash = hash,
                    Size = size
                });
            if (result == default)
                return null;

            return new ArchiveDownload
            {
                Id = result.Item1,
                IsFailed = result.Item4,
                DownloadFinished = result.Item6,
                Archive = new Archive(result.Item5) {Size = result.Item2 ?? 0, Hash = result.Item3 ?? default}
            };

        }

        
        public async Task<ArchiveDownload> GetOrEnqueueArchive(Archive a)
        {
            await using var conn = await Open();
            await using var trans = await conn.BeginTransactionAsync();
            var result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, bool?, AbstractDownloadState, DateTime?)>(
                "SELECT Id, Size, Hash, IsFailed, DownloadState, DownloadFinished FROM dbo.ArchiveDownloads WHERE PrimaryKeyString = @PrimaryKeyString AND Hash = @Hash AND Size = @Size",
                new
                {
                    PrimaryKeyString = a.State.PrimaryKeyString,
                    Hash = a.Hash,
                    Size = a.Size
                }, trans);
            if (result.Item1 != default)
            {
                return new ArchiveDownload
                {
                    Id = result.Item1,
                    IsFailed = result.Item4,
                    DownloadFinished = result.Item6,
                    Archive = new Archive(result.Item5) {Size = result.Item2 ?? 0, Hash = result.Item3 ?? default}
                };
            }

            var id = Guid.NewGuid();
            await conn.ExecuteAsync(
                "INSERT INTO ArchiveDownloads (Id, PrimaryKeyString, Size, Hash, DownloadState, Downloader) VALUES (@Id, @PrimaryKeyString, @Size, @Hash, @DownloadState, @Downloader)",
                new
                {
                    Id = id,
                    PrimaryKeyString = a.State.PrimaryKeyString,
                    Size = a.Size == 0 ? null : (long?)a.Size,
                    Hash = a.Hash == default ? null : (Hash?)a.Hash,
                    DownloadState = a.State,
                    Downloader = AbstractDownloadState.TypeToName[a.State.GetType()]
                }, trans);

            await trans.CommitAsync();

            return new ArchiveDownload {Id = id, Archive = a,};

        }

        public async Task<ArchiveDownload> GetNextPendingDownload(bool ignoreNexus = false)
        {
            await using var conn = await Open();
            (Guid, long?, Hash?, AbstractDownloadState) result;

            if (ignoreNexus)
            {
                result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, AbstractDownloadState)>(
                    "SELECT TOP(1) Id, Size, Hash, DownloadState FROM dbo.ArchiveDownloads WHERE DownloadFinished is NULL AND Downloader != 'NexusDownloader+State'");
            }
            else
            {
                result = await conn.QueryFirstOrDefaultAsync<(Guid, long?, Hash?, AbstractDownloadState)>(
                    "SELECT TOP(1) Id, Size, Hash, DownloadState FROM dbo.ArchiveDownloads WHERE DownloadFinished is NULL");
            }

            if (result == default)
                return null;
            
            return new ArchiveDownload
            {
                Id = result.Item1,
                Archive = new Archive(result.Item4) {Size = result.Item2 ?? 0, Hash = result.Item3 ?? default,},
            };
        }
        
        public async Task UpdatePendingDownload(ArchiveDownload ad)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(
                "UPDATE dbo.ArchiveDownloads SET IsFailed = @IsFailed, DownloadFinished = @DownloadFinished, Hash = @Hash, Size = @Size, FailMessage = @FailMessage WHERE Id = @Id",
                new
                {
                    Id = ad.Id,
                    IsFailed = ad.IsFailed,
                    DownloadFinished = ad.DownloadFinished,
                    Size = ad.Archive.Size,
                    Hash = ad.Archive.Hash,
                    FailMessage = ad.FailMessage
                });
        }

        public async Task<int> EnqueueModListFilesForIndexing()
        {
            await using var conn = await Open();
            return await conn.ExecuteAsync(@"
            INSERT INTO dbo.ArchiveDownloads (Id, PrimaryKeyString, Hash, DownloadState, Size, Downloader)
            SELECT DISTINCT NEWID(), mla.PrimaryKeyString, mla.Hash, mla.State, mla.Size, SUBSTRING(mla.PrimaryKeyString, 0, CHARINDEX('|', mla.PrimaryKeyString))
            FROM [dbo].[ModListArchives] mla
                LEFT JOIN dbo.ArchiveDownloads ad on mla.PrimaryKeyString = ad.PrimaryKeyString AND mla.Hash = ad.Hash
            WHERE ad.PrimaryKeyString is null");
        }

        public async Task<List<Archive>> GetGameFiles(Game game, string version)
        {
            await using var conn = await Open();
            var files = (await conn.QueryAsync<(Hash, long, AbstractDownloadState)>(
                $"SELECT Hash, Size, DownloadState FROM dbo.ArchiveDownloads WHERE PrimaryKeyString like 'GameFileSourceDownloader+State|{game}|{version}|%'"))
                .Select(f => new Archive(f.Item3)
                {
                    Hash = f.Item1,
                    Size = f.Item2
                }).ToList();
            return files;
        }

        public async Task<Archive[]> ResolveDownloadStatesByHash(Hash hash)
        {
            await using var conn = await Open();
            var files = (await conn.QueryAsync<(long, Hash, AbstractDownloadState)>(
                    @"SELECT Size, Hash,  DownloadState from dbo.ArchiveDownloads WHERE Hash = @Hash AND IsFailed = 0 AND DownloadFinished IS NOT NULL ORDER BY DownloadFinished DESC",
                    new {Hash = hash})
                );
            return files.Select(e =>
                new Archive(e.Item3) {Size = e.Item1, Hash = e.Item2}
            ).ToArray();
        }
    }
}
