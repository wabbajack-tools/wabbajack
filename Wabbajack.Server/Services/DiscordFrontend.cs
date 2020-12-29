using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using OMODFramework;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack.Server.Services
{
    public class DiscordFrontend : IStartable
    {
        private ILogger<DiscordFrontend> _logger;
        private AppSettings _settings;
        private QuickSync _quickSync;
        private DiscordSocketClient _client;
        private SqlService _sql;

        public DiscordFrontend(ILogger<DiscordFrontend> logger, AppSettings settings, QuickSync quickSync, SqlService sql)
        {
            _logger = logger;
            _settings = settings;
            _quickSync = quickSync;
            
            _client = new DiscordSocketClient();

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;

            _sql = sql;
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
                    await PurgeNexusCache(arg, parts[2]);
                }
                else if (parts[1] == "cyberpunk")
                {
                    var random = new Random();
                    var releaseDate = new DateTime(2020, 12, 10, 0, 0, 0, DateTimeKind.Utc);
                    var r = releaseDate - DateTime.UtcNow;
                    if (r < TimeSpan.Zero)
                    {
                        await ReplyTo(arg, "It's out, what are you doing here?");
                    }
                    else
                    {
                        var msgs = (await "cyberpunk_message.txt".RelativeTo(AbsolutePath.EntryPoint)
                            .ReadAllLinesAsync()).ToArray();
                        var msg = msgs[random.Next(0, msgs.Length)];
                        var fullmsg = String.Format(msg,
                            $"{r.Days} days, {r.Hours} hours, {r.Minutes} minutes, {r.Seconds} seconds");
                        await ReplyTo(arg, fullmsg);
                    }
                }
            }
        }

        private async Task PurgeNexusCache(SocketMessage arg, string mod)
        {
            if (Uri.TryCreate(mod, UriKind.Absolute, out var url))
            {
                mod = Enumerable.Last(url.AbsolutePath.Split("/", StringSplitOptions.RemoveEmptyEntries));
            }
            
            if (int.TryParse(mod, out var mod_id))
            {
                await _sql.PurgeNexusCache(mod_id);
                await _quickSync.Notify<ListValidator>();
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

        public void Start()
        {
            _client.LoginAsync(TokenType.Bot, Utils.FromEncryptedJson<string>("discord-key").Result).Wait();
            _client.StartAsync().Wait();
        }
        
    }
}
