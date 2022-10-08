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

namespace Wabbajack.Server.Services;

public enum Channel
{
    // High volume messaging, really only useful for internal devs
    Spam,

    // Low volume messages designed for admins
    Ham
}

public class DiscordWebHook : AbstractService<DiscordWebHook, int>
{
    private readonly HttpClient _client;
    private readonly DTOSerializer _dtos;
    private readonly Random _random = new();

    public DiscordWebHook(ILogger<DiscordWebHook> logger, AppSettings settings, QuickSync quickSync, HttpClient client,
        DTOSerializer dtos) : base(logger, settings, quickSync, TimeSpan.FromHours(1))
    {
        _settings = settings;
        _logger = logger;
        _client = client;
        _dtos = dtos;

        Task.Run(async () =>
        {

            var message = new DiscordMessage
            {
                Content = $"\"{await GetQuote()}\" - Sheogorath (as he brings the server online)"
            };
            await Send(Channel.Ham, message);
            await Send(Channel.Spam, message);
        });
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
            if (string.IsNullOrWhiteSpace(url)) return;

            await _client.PostAsync(url,
                new StringContent(_dtos.Serialize(message), Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While sending discord message");
        }
    }

    private async Task<string> GetQuote()
    {
        var lines =
            await Assembly.GetExecutingAssembly()!.GetManifestResourceStream("Wabbajack.Server.sheo_quotes.txt")!
                .ReadLinesAsync()
                .ToList();
        return lines[_random.Next(lines.Count)].Trim();
    }

    public override async Task<int> Execute()
    {
        return 0;
    }
}