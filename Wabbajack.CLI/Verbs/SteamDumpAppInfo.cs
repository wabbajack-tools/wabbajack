using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SteamKit2;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Wabbajack.CLI.Verbs;

public class SteamDumpAppInfo : IVerb
{
    private readonly ILogger<SteamDumpAppInfo> _logger;
    private readonly Client _client;
    private readonly ITokenProvider<SteamLoginState> _token;
    private readonly DepotDownloader _downloader;
    private readonly DTOSerializer _dtos;

    public SteamDumpAppInfo(ILogger<SteamDumpAppInfo> logger, Client steamClient, ITokenProvider<SteamLoginState> token, 
        DepotDownloader downloader, DTOSerializer dtos)
    {
        _logger = logger;
        _client = steamClient;
        _token = token;
        _downloader = downloader;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-app-dump-info",
        "Dumps information to the console about the given app", new[]
        {
            new OptionDefinition(typeof(string), "g", "game", "Wabbajack game name")
        });

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
        var appId = (uint) game.SteamIDs.First();

        if (!await _downloader.AccountHasAccess(appId))
        {
            _logger.LogError("Your account does not have access to this Steam App");
            return 1;
        }

        var appData = await _downloader.GetAppInfo((uint)game.SteamIDs.First());

        Console.WriteLine("App Depots: ");
        
        Console.WriteLine(_dtos.Serialize(appData, true));

        return 0;
    }

    
}