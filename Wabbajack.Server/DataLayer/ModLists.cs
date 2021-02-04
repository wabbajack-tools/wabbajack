using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task IngestModList(Hash hash, ModlistMetadata metadata, ModList modlist, bool brokenDownload)
        {
            await using var conn = await Open();
            await using var tran = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync(@"DELETE FROM dbo.ModLists Where MachineUrl = @MachineUrl",
                new {MachineUrl = metadata.Links.MachineURL}, tran);

            var archives = modlist.Archives;
            var directives = modlist.Directives;
            modlist.Archives = new List<Archive>();
            modlist.Directives = new List<Directive>();

            await conn.ExecuteAsync(
                @"INSERT INTO dbo.ModLists (MachineUrl, Hash, Metadata, ModList, BrokenDownload) VALUES (@MachineUrl, @Hash, @Metadata, @ModList, @BrokenDownload)",
                new
                {
                    MachineUrl = metadata.Links.MachineURL,
                    Hash = hash,
                    MetaData = metadata.ToJson(),
                    ModList = modlist.ToJson(),
                    BrokenDownload = brokenDownload
                }, tran);
            
            var entries = archives.Select(a =>
                new
                {
                    MachineUrl = metadata.Links.MachineURL,
                    Name = a.Name,
                    Hash = a.Hash,
                    Size = a.Size,
                    State = a.State.ToJson(),
                    PrimaryKeyString = a.State.PrimaryKeyString
                }).ToArray();

            await conn.ExecuteAsync(@"DELETE FROM dbo.ModListArchives WHERE MachineURL = @machineURL",
                new {MachineUrl = metadata.Links.MachineURL}, tran);
            
            foreach (var entry in entries)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO dbo.ModListArchives (MachineURL, Name, Hash, Size, PrimaryKeyString, State) VALUES (@MachineURL, @Name, @Hash, @Size, @PrimaryKeyString, @State)",
                    entry, tran);
            }
            
            await tran.CommitAsync();
        }
        
        public async Task<bool> HaveIndexedModlist(string machineUrl, Hash hash)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT MachineURL from dbo.Modlists WHERE MachineURL = @MachineUrl AND Hash = @Hash",
                new {MachineUrl = machineUrl, Hash = hash});
            return result != null;
        }

        public async Task<bool> HashIsInAModlist(Hash hash)
        {
            await using var conn = await Open();
            var result = await conn.QueryFirstOrDefaultAsync<bool>("SELECT Hash FROM dbo.ModListArchives Where Hash = @Hash",
                new {Hash = hash});
            return result;
        }

        public async Task<List<Archive>> ModListArchives(string machineURL)
        {
            await using var conn = await Open();
            var archives = await conn.QueryAsync<(string, Hash, long, AbstractDownloadState)>("SELECT Name, Hash, Size, State FROM dbo.ModListArchives WHERE MachineUrl = @MachineUrl",
                new {MachineUrl = machineURL});
            return archives.Select(t => new Archive(t.Item4)
            {
                Name = string.IsNullOrWhiteSpace(t.Item1) ? t.Item4.PrimaryKeyString : t.Item1, 
                Size = t.Item3, 
                Hash = t.Item2
            }).ToList();
        }
        
        public async Task<List<Archive>> ModListArchives()
        {
            await using var conn = await Open();
            var archives = await conn.QueryAsync<(string, Hash, long, AbstractDownloadState)>("SELECT Name, Hash, Size, State FROM dbo.ModListArchives");
            return archives.Select(t => new Archive(t.Item4)
            {
                Name = string.IsNullOrWhiteSpace(t.Item1) ? t.Item4.PrimaryKeyString : t.Item1, 
                Size = t.Item3, 
                Hash = t.Item2
            }).ToList();
        }

        public async Task<int> PurgeList(string machineURL)
        {
            await using var conn = await Open();
            var ret1 = await conn.ExecuteAsync(@" delete from dbo.ModListArchives where MachineURL = @machineURL",
                new {machineURL});
            var ret2 = await conn.ExecuteAsync(@" delete from dbo.ModLists where MachineURL = @machineURL",
                new {machineURL});
            return ret1 + ret2;
        }
    }
}
