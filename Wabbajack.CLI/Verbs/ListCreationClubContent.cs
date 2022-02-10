using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class ListCreationClubContent : IVerb
{
    private readonly ILogger<ListCreationClubContent> _logger;
    private readonly Client _client;

    public ListCreationClubContent(ILogger<ListCreationClubContent> logger, Client wjClient)
    {
        _logger = logger;
        _client = wjClient;
    }
    public Command MakeCommand()
    {
        var command = new Command("list-creation-club-content");
        command.Description = "Lists all known creation club content";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(CancellationToken token)
    {
        _logger.LogInformation("Getting list of content");
        var skyrimContent = (await _client.ListContent(Game.SkyrimSpecialEdition, token))
            .Select(f => (Game.SkyrimSpecialEdition, f));
        var falloutContent = (await _client.ListContent(Game.Fallout4, token))
            .Select(f => (Game.Fallout4, f));

        foreach (var (game, content) in skyrimContent.Concat(falloutContent).OrderBy(f => f.f.Name))
        {
            Console.WriteLine($"Game: {game}");
            Console.WriteLine($"Name: {content.Name}");
            Console.WriteLine($"Download Size: {content.DepotSize.ToFileSizeString()}");
            Console.WriteLine($"Uri: bethesda://{game}/{content.ContentId}");
            Console.WriteLine("-----------------------------------");
        }

        return 0;
    }
}