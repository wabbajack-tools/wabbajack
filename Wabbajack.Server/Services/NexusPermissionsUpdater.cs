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
    public class NexusPermissionsUpdater : AbstractService<NexusKeyMaintainance, int>
    {
        private DiscordWebHook _discord;
        private SqlService _sql;
        
        public static TimeSpan MaxSync = TimeSpan.FromHours(4);

        public NexusPermissionsUpdater(ILogger<NexusKeyMaintainance> logger, AppSettings settings, QuickSync quickSync, DiscordWebHook discord, SqlService sql) : base(logger, settings, quickSync, TimeSpan.FromSeconds(1))
        {
            _discord = discord;
            _sql = sql;
        }

        public override async Task<int> Execute()
        {
            await _sql.UpdateGameMetadata();
            
            
            var data = await _sql.ModListArchives();
            var nexusArchives = data.Select(a => a.State).OfType<NexusDownloader.State>().Select(d => (d.Game, d.ModID))
                .Distinct()
                .ToList();
            
            _logger.LogInformation($"Starting nexus permissions updates for {nexusArchives.Count} mods");
            
            using var queue = new WorkQueue(1);

            var prev = await _sql.GetNexusPermissions();

            var lag = MaxSync / nexusArchives.Count * 2; 


            await nexusArchives.PMap(queue, async archive =>
            {
                _logger.LogInformation($"Checking permissions for {archive.Game} {archive.ModID}");
                var result = await HTMLInterface.GetUploadPermissions(archive.Game, archive.ModID);
                await _sql.SetNexusPermission(archive.Game, archive.ModID, result);
                
                if (prev.TryGetValue((archive.Game, archive.ModID), out var oldPermission))
                {
                    if (oldPermission != result)
                    {
                        await _discord.Send(Channel.Spam,
                            new DiscordMessage {
                                Content = $"Permissions status of {archive.Game} {archive.ModID} was {oldPermission} is now {result}"
                            });
                        await _sql.PurgeNexusCache(archive.ModID);
                        await _quickSync.Notify<ListValidator>();
                    }
                }
                
                await Task.Delay(lag);
            });

            return 1;
        }

    }
}
