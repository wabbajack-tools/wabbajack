using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class NexusPermissionsUpdater : AbstractService<NexusKeyMaintainance, int>
    {
        private DiscordWebHook _discord;
        private SqlService _sql;

        public NexusPermissionsUpdater(ILogger<NexusKeyMaintainance> logger, AppSettings settings, QuickSync quickSync, DiscordWebHook discord, SqlService sql) : base(logger, settings, quickSync, TimeSpan.FromHours(4))
        {
            _discord = discord;
            _sql = sql;
        }

        public override async Task<int> Execute()
        {
            var permissions = await _sql.GetNexusPermissions();

            var data = await _sql.ModListArchives();
            var nexusArchives = data.Select(a => a.State).OfType<NexusDownloader.State>().Select(d => (d.Game, d.ModID))
                .Distinct()
                .ToList();
            
            _logger.LogInformation($"Starting nexus permissions updates for {nexusArchives.Count} mods");
            
            using var queue = new WorkQueue();

            var results = await nexusArchives.PMap(queue, async archive =>
            {
                var permissions = await HTMLInterface.GetUploadPermissions(archive.Game, archive.ModID);
                return (archive.Game, archive.ModID, permissions);
            });

            var updated = 0;
            foreach (var result in results)
            {
                if (permissions.TryGetValue((result.Game, result.ModID), out var oldPermission))
                {
                    if (oldPermission != result.permissions)
                    {
                        await _discord.Send(Channel.Spam,
                            new DiscordMessage {
                                Content = $"Permissions status of {result.Game} {result.ModID} was {oldPermission} is now {result.permissions} "
                            });
                        await _sql.PurgeNexusCache(result.ModID);
                        updated += 1;
                    }
                }
            }

            await _sql.SetNexusPermissions(results);

            if (updated > 0)
                await _quickSync.Notify<ListValidator>();


            return updated;
        }
    }
}
