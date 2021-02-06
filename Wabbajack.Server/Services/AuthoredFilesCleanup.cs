using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using WebSocketSharp;

namespace Wabbajack.Server.Services
{
    public class AuthoredFilesCleanup : AbstractService<AuthoredFilesCleanup, int>
    {
        private SqlService _sql;
        private DiscordWebHook _discord;

        public AuthoredFilesCleanup(ILogger<AuthoredFilesCleanup> logger, AppSettings settings, QuickSync quickSync, SqlService sql, DiscordWebHook discord) : base(logger, settings, quickSync, TimeSpan.FromHours(6))
        {
            _sql = sql;
            _discord = discord;
        }

        public override async Task<int> Execute()
        {

            var toDelete = await FindFilesToDelete();

            var log = new[] {$"CDNDelete ({toDelete.CDNDelete.Length}):\n\n"}
                .Concat(toDelete.CDNDelete)
                .Concat(new[] {$"SQLDelete ({toDelete.SQLDelete.Length}"})
                .Concat(toDelete.SQLDelete)
                .Concat(new[] {$"CDNRemain ({toDelete.CDNNotDeleted.Length}"})
                .Concat(toDelete.CDNNotDeleted)
                .Concat(new[] {$"SQLRemain ({toDelete.SQLNotDeleted.Length}"})
                .Concat(toDelete.SQLNotDeleted)
                .ToArray();

            //await AbsolutePath.EntryPoint.Combine("cdn_delete_log.txt").WriteAllLinesAsync(log);
            
            
            foreach (var sqlFile in toDelete.SQLDelete)
            {
                Utils.Log($"Deleting {sqlFile} from SQL");
                await _sql.DeleteFileDefinition(await _sql.GetCDNFileDefinition(sqlFile));
            }


            using var queue = new WorkQueue(6);
            await toDelete.CDNDelete.Select((d, idx) => (d, idx)).PMap(queue, async cdnFile =>
            {
                using var conn = await (await BunnyCdnFtpInfo.GetCreds(StorageSpace.AuthoredFiles)).GetClient();
                Utils.Log($"Deleting {cdnFile} from CDN");
                await _discord.Send(Channel.Ham,
                    new DiscordMessage
                    {
                        Content =
                            $"({cdnFile.idx}/{toDelete.CDNDelete.Length}) {cdnFile.d} is no longer referenced by any modlist and will be removed from the CDN"
                    });
                if (await conn.DirectoryExistsAsync(cdnFile.d))
                    await conn.DeleteDirectoryAsync(cdnFile.d);

                if (await conn.FileExistsAsync(cdnFile.d))
                    await conn.DeleteFileAsync(cdnFile.d);
            });
            return toDelete.CDNDelete.Length + toDelete.SQLDelete.Length;
            
        }

        public async Task<(string[] CDNDelete, string[] SQLDelete, string[] CDNNotDeleted, string[] SQLNotDeleted)> FindFilesToDelete()
        {
            var cdnNames = (await GetCDNMungedNames()).ToHashSet();
            var usedNames = (await GetUsedCDNFiles()).ToHashSet();
            var sqlFiles = (await _sql.AllAuthoredFiles()).ToDictionary(f => f.MungedName);
            var keep = GetKeepList(cdnNames, usedNames, sqlFiles).ToHashSet();

            var cdnDelete = cdnNames.Where(h => !keep.Contains(h)).ToArray();
            var sqlDelete = sqlFiles.Where(s => !keep.Contains(s.Value.MungedName))
                .Select(s => s.Value.ServerAssignedUniqueId)
                .ToArray();

            var cdnhs = cdnDelete.ToHashSet();
            var notDeletedCDN = cdnNames.Where(f => !cdnhs.Contains(f)).ToArray();
            var sqlhs = sqlDelete.ToHashSet();
            var sqlNotDeleted = sqlFiles.Where(f => !sqlDelete.Contains(f.Value.ServerAssignedUniqueId))
                .Select(f => f.Value.MungedName)
                .ToArray();
            return (cdnDelete, sqlDelete, notDeletedCDN, sqlNotDeleted);
        }

        private IEnumerable<string> GetKeepList(HashSet<string> cdnNames, HashSet<string> usedNames, Dictionary<string, AuthoredFilesSummary> sqlFiles)
        {
            var cutOff = DateTime.UtcNow - TimeSpan.FromDays(7);
            foreach (var file in sqlFiles.Where(f => f.Value.LastTouched > cutOff))
                yield return file.Value.MungedName;

            foreach (var file in usedNames)
                yield return file;
        }

        public async Task<string[]> GetCDNMungedNames()
        {
            using var client = await (await BunnyCdnFtpInfo.GetCreds(StorageSpace.AuthoredFiles)).GetClient();
            var lst = await client.GetListingAsync(@"\");
            return lst.Select(l => l.Name).ToArray();
        }

        public async Task<string[]> GetUsedCDNFiles()
        {
            var modlists = (await ModlistMetadata.LoadFromGithub())
                .Concat((await ModlistMetadata.LoadUnlistedFromGithub()))
                .Select(f => f.Links.Download)
                .Where(f => f.StartsWith(Consts.WabbajackAuthoredFilesPrefix))
                .Select(f => f.Substring(Consts.WabbajackAuthoredFilesPrefix.Length));

            var files = (await _sql.ModListArchives())
                .Select(a => a.State)
                .OfType<WabbajackCDNDownloader.State>()
                .Select(s => s.Url.ToString().Substring(Consts.WabbajackAuthoredFilesPrefix.Length));
            
            

            var names = modlists.Concat(files).Distinct().ToArray();
            var namesBoth = names.Concat(names.Select(HttpUtility.UrlDecode))
                .Concat(names.Select(HttpUtility.UrlEncode))
                .Distinct()
                .ToArray();
            return namesBoth;
        }
    }
}
