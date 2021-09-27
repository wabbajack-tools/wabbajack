using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
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
        private Random _random = new();
        private readonly HttpClient _client;
        private readonly DTOSerializer _dtos;

        public DiscordWebHook(ILogger<DiscordWebHook> logger, AppSettings settings, QuickSync quickSync, HttpClient client, DTOSerializer dtos) : base(logger, settings, quickSync, TimeSpan.FromHours(1))
        {
            _settings = settings;
            _logger = logger;
            _client = client;
            _dtos = dtos;

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
                var url = channel switch
                {
                    Channel.Spam => _settings.SpamWebHook,
                    Channel.Ham => _settings.HamWebHook,
                    _ => null
                };
                if (url == null) return;
                
                await _client.PostAsync(url, new StringContent(_dtos.Serialize(message), Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While sending discord message");
            }
        }

        private async Task<string> GetQuote()
        {
            var lines = await Assembly.GetExecutingAssembly()!.GetManifestResourceStream("Wabbajack.Server.sheo_quotes.txt")!
                .ReadLinesAsync()
                .ToList();
            return lines[_random.Next(lines.Count)].Trim();
        }

        public override async Task<int> Execute()
        {
            return 0;
        }
    }
}
