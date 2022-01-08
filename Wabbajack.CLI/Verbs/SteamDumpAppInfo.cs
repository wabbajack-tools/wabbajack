using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam;

namespace Wabbajack.CLI.Verbs;

public class SteamAppDumpInfo : IVerb
{
    private readonly ILogger<SteamAppDumpInfo> _logger;
    private readonly Client _client;
    private readonly ITokenProvider<SteamLoginState> _token;
    private readonly DepotDownloader _downloader;

    public SteamAppDumpInfo(ILogger<SteamAppDumpInfo> logger, Client steamClient, ITokenProvider<SteamLoginState> token, 
        DepotDownloader downloader)
    {
        _logger = logger;
        _client = steamClient;
        _token = token;
        _downloader = downloader;
    }
    public Command MakeCommand()
    {
        var command = new Command("steam-app-dump-info");
        command.Description = "Dumps information to the console about the given app";
        
        command.Add(new Option<string>(new[] {"-g", "-game", "-gameName"}, "Wabbajack game name"));
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(string gameName)
    {
        if (!GameRegistry.TryGetByFuzzyName(gameName, out var game))
        {
            _logger.LogError("Can't find game {GameName} in game registry", gameName);
            return 1;
        }

        await _client.Login();

        if (!await _downloader.AccountHasAccess((uint) game.SteamIDs.First()))
        {
            _logger.LogError("Your account does not have access to this Steam App");
            return 1;
        }

        return 0;
    }
    
}