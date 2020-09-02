using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public enum Channel
    {
        // High volume messaging, really only useful for internal devs
        Spam,
        // Low volume messages designed for admins
        Ham
    }
    public class DiscordWebHook : AbstractService<DiscordWebHook, int>
    {
        private AppSettings _settings;
        private ILogger<DiscordWebHook> _logger;
        private Random _random = new Random();

        public DiscordWebHook(ILogger<DiscordWebHook> logger, AppSettings settings, QuickSync quickSync) : base(logger, settings, quickSync, TimeSpan.FromHours(1))
        {
            _settings = settings;
            _logger = logger;

            var message = new DiscordMessage
            {
                Content = $"\"{GetQuote()}\" - Sheogorath (as he brings the server online)",
            }; 
            var a = Send(Channel.Ham, message);
            var b = Send(Channel.Spam, message);

        }

        public async Task Send(Channel channel, DiscordMessage message)
        {
            try
            {
                string url = channel switch
                {
                    Channel.Spam => _settings.SpamWebHook,
                    Channel.Ham => _settings.HamWebHook,
                    _ => null
                };
                if (url == null) return;

                var client = new Wabbajack.Lib.Http.Client();
                await client.PostAsync(url, new StringContent(message.ToJson(true), Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.ToString());
            }
        }

        private string GetQuote()
        {
            var data = Assembly.GetExecutingAssembly()!.GetManifestResourceStream("Wabbajack.Server.sheo_quotes.txt");
            var lines = Encoding.UTF8.GetString(data.ReadAll()).Split('\n');
            return lines[_random.Next(lines.Length)].Trim();
        }

        public override async Task<int> Execute()
        {
            return 0;
        }
    }
}
