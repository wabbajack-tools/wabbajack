using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.DTOs;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.TokenProviders;

namespace Wabbajack.Server.Services;

public class DiscordFrontend : IStartable
{
    private readonly IDiscordToken _token;
    private readonly DiscordSocketClient _client;
    private readonly MetricsKeyCache _keyCache;
    private readonly ILogger<DiscordFrontend> _logger;
    private readonly QuickSync _quickSync;
    private AppSettings _settings;
    private readonly SqlService _sql;

    public DiscordFrontend(ILogger<DiscordFrontend> logger, AppSettings settings, QuickSync quickSync, SqlService sql,
        MetricsKeyCache keyCache, IDiscordToken token)
    {
        _logger = logger;
        _settings = settings;
        _quickSync = quickSync;

        _client = new DiscordSocketClient();

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;

        _sql = sql;
        _keyCache = keyCache;
        _token = token;
    }

    public async Task Start()
    {
        await _client.LoginAsync(TokenType.Bot, await _token.Get());
        await _client.StartAsync();
    }

    private async Task MessageReceivedAsync(SocketMessage arg)
    {
        _logger.LogInformation(arg.Content);
        if (arg.Content.StartsWith("!dervenin"))
        {
            var parts = arg.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (parts[0] != "!dervenin")
                return;

            if (parts.Length == 1) await ReplyTo(arg, "Wat?");

            if (parts[1] == "purge-nexus-cache")
            {
                if (parts.Length != 3)
                {
                    await ReplyTo(arg, "Welp you did that wrong, gotta give me a mod-id or url");
                    return;
                }

                await PurgeNexusCache(arg, parts[2]);
            }
            else if (parts[1] == "quick-sync")
            {
                var options = await _quickSync.Report();
                if (parts.Length != 3)
                {
                    var optionsStr = string.Join(", ", options.Select(o => o.Key.Name));
                    await ReplyTo(arg, $"Can't expect me to quicksync the whole damn world! Try: {optionsStr}");
                }
                else
                {
                    foreach (var pair in options.Where(o => o.Key.Name == parts[2]))
                    {
                        await _quickSync.Notify(pair.Key);
                        await ReplyTo(arg, $"Notified {pair.Key}");
                    }
                }
            }
            else if (parts[1] == "purge-list")
            {
                if (parts.Length != 3)
                {
                    await ReplyTo(arg, "Yeah, I'm not gonna purge the whole server...");
                }
                else
                {
                    var deleted = await _sql.PurgeList(parts[2]);
                    await _quickSync.Notify<ModListDownloader>();
                    await ReplyTo(arg,
                        $"Purged all traces of #{parts[2]} from the server, triggered list downloading. {deleted} records removed");
                }
            }
            else if (parts[1] == "mirror-mod")
            {
                await MirrorModCommand(arg, parts);
            }
            else if (parts[1] == "users")
            {
                await ReplyTo(arg, $"Wabbajack has {await _keyCache.KeyCount()} known unique users");
            }
        }
    }

    private async Task MirrorModCommand(SocketMessage msg, string[] parts)
    {
        if (parts.Length != 2)
        {
            await ReplyTo(msg, "Command is: mirror-mod <game-name> <mod-id>");
            return;
        }

        if (long.TryParse(parts[2], out var modId))
        {
            await ReplyTo(msg, $"Got {modId} for a mod-id, expected a integer");
            return;
        }

        if (GameRegistry.TryGetByFuzzyName(parts[1], out var game))
        {
            var gameNames = GameRegistry.Games.Select(g => g.Value.NexusName)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => (string) g)
                .ToHashSet();
            var joined = string.Join(", ", gameNames.OrderBy(g => g));
            await ReplyTo(msg, $"Got {parts[1]} for a game name, expected something like: {joined}");
        }

        if (game!.NexusGameId == default) await ReplyTo(msg, $"No NexusGameID found for {game}");

        await _sql.AddNexusModWithOpenPerms(game.Game, modId);
        await _quickSync.Notify<MirrorUploader>();
        await ReplyTo(msg, "Done, and I notified the uploader");
    }

    private async Task PurgeNexusCache(SocketMessage arg, string mod)
    {
        if (Uri.TryCreate(mod, UriKind.Absolute, out var url))
            mod = url.AbsolutePath.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();

        if (int.TryParse(mod, out var mod_id))
        {
            await _sql.PurgeNexusCache(mod_id);
            await ReplyTo(arg, $"It is done, {mod_id} has been purged, list validation has been triggered");
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