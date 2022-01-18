using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;

namespace Wabbajack.Server.Services;

public class DiscordBackend
{
    private readonly AppSettings _settings;
    private readonly ILogger<DiscordBackend> _logger;
    private readonly DiscordSocketClient _client;
    private readonly NexusCacheManager _nexusCacheManager;

    public DiscordBackend(ILogger<DiscordBackend> logger, AppSettings settings, NexusCacheManager nexusCacheManager)
    {
        _settings = settings;
        _logger = logger;
        _nexusCacheManager = nexusCacheManager;
        _client = new DiscordSocketClient(new DiscordSocketConfig()
        {
            
        });
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        Task.Run(async () =>
        {
            await _client.LoginAsync(TokenType.Bot, settings.DiscordKey);
            await _client.StartAsync();
        });
    }

    private async Task MessageReceivedAsync(SocketMessage arg)
    {
        _logger.LogInformation(arg.Content);
        
        if (arg.Content.StartsWith("!dervenin"))
        {
            var parts = arg.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (parts[0] != "!dervenin")
                return;

            if (parts.Length == 1)
            {
                await ReplyTo(arg, "Wat?");
            }

            if (parts[1] == "purge-nexus-cache")
            {
                if (parts.Length != 3)
                {
                    await ReplyTo(arg, "Welp you did that wrong, gotta give me a mod-id or url");
                    return;
                }
                var rows = await _nexusCacheManager.Purge(parts[2]);
                await ReplyTo(arg, $"Purged {rows} rows");
            }

            if (parts[1] == "nft")
            {
                await ReplyTo(arg, "No Fucking Thanks.");
            }
          
        }
    }
    
    private async Task ReplyTo(SocketMessage socketMessage, string message)
    {
        await socketMessage.Channel.SendMessageAsync(message);
    }


    private async Task ReadyAsync()
    {
    }

    private async Task LogAsync(LogMessage arg)
    {
        switch (arg.Severity)
        {
            case LogSeverity.Info:
                _logger.LogInformation(arg.Message);
                break;
            case LogSeverity.Warning:
                _logger.LogWarning(arg.Message);
                break;
            case LogSeverity.Critical:
                _logger.LogCritical(arg.Message);
                break;
            case LogSeverity.Error:
                _logger.LogError(arg.Exception, arg.Message);
                break;
            case LogSeverity.Verbose:
                _logger.LogTrace(arg.Message);
                break;
            case LogSeverity.Debug:
                _logger.LogDebug(arg.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}