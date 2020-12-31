using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class Watchdog : AbstractService<Watchdog, int>
    {
        private DiscordWebHook _discord;

        public Watchdog(ILogger<Watchdog> logger, AppSettings settings, QuickSync quickSync, DiscordWebHook discordWebHook) : base(logger, settings, quickSync, TimeSpan.FromMinutes(5))
        {
            _discord = discordWebHook;
        }

        public override async Task<int> Execute()
        {
            var report = await _quickSync.Report();
            foreach (var service in report)
            {
                if (service.Value.LastRunTime != default && service.Value.LastRunTime >= service.Value.Delay * 4)
                {
                    await _discord.Send(Channel.Spam,
                        new DiscordMessage {Content = $"Service {service.Key.Name} has missed it's scheduled execution window. \n Current work: \n {string.Join("\n", service.Value.ActiveWork)}"});
                }
            }

            return report.Count;
        }
    }
}
