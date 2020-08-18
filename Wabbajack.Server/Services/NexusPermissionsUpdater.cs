using System;
using System.Linq;
using System.Threading;
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
    public class NexusPermissionsUpdater : AbstractService<NexusPermissionsUpdater, int>
    {
        private DiscordWebHook _discord;
        private SqlService _sql;
        
        public NexusPermissionsUpdater(ILogger<NexusPermissionsUpdater> logger, AppSettings settings, QuickSync quickSync, DiscordWebHook discord, SqlService sql) : base(logger, settings, quickSync, TimeSpan.FromMinutes(5))
        {
            _discord = discord;
            _sql = sql;
        }

        public override async Task<int> Execute()
        {
            await _sql.UpdateGameMetadata();
            
            
            var data = await _sql.ModListArchives();
            var nexusArchives = data.Select(a => a.State).OfType<NexusDownloader.State>().Select(d => (d.Game, d.ModID))
                .Where(g => g.Game.MetaData().NexusGameId != 0)
                .Distinct()
                .ToList();
            
            _logger.LogInformation($"Starting nexus permissions updates for {nexusArchives.Count} mods");
            
            using var queue = new WorkQueue();

            var prev = await _sql.GetHiddenNexusMods();
            _logger.LogInformation($"Found {prev.Count} hidden nexus mods to check");

            await prev.PMap(queue, async archive =>
            {
                var (game, modID) = archive.Key;
                _logger.LogInformation($"Checking permissions for {game} {modID}");
                var result = await HTMLInterface.GetUploadPermissions(game, modID);
                await _sql.SetNexusPermission(game, modID, result);
                
                if (archive.Value != result)
                {
                    await _discord.Send(Channel.Ham,
                        new DiscordMessage {
                            Content = $"Permissions status of {game} {modID} was {archive.Value} is now {result}"
                        });
                    await _sql.PurgeNexusCache(modID);
                    await _quickSync.Notify<ListValidator>();
                }
            });

            return 1;
        }

    }
}
