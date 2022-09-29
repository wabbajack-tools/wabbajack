using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Bethesda;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins.BethesdaNet;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class ListCreationClubContent : IVerb
{
    private readonly ILogger<ListCreationClubContent> _logger;
    private readonly Client _client;
    private readonly BethesdaDownloader _downloader;

    public ListCreationClubContent(ILogger<ListCreationClubContent> logger, Client wjClient, Wabbajack.Downloaders.Bethesda.BethesdaDownloader downloader)
    {
        _logger = logger;
        _client = wjClient;
        _downloader = downloader;
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

        foreach (var (game, content) in skyrimContent.Concat(falloutContent).OrderBy(f => f.f.Content.Name))
        {
            Console.WriteLine($"Game: {game}");
            Console.WriteLine($"Name: {content.Content.Name}");
            Console.WriteLine($"Download Size: {content.Content.DepotSize.ToFileSizeString()}");
            Console.WriteLine($"Uri: {_downloader.UnParse(content.State)}");
            Console.WriteLine("-----------------------------------");
        }

        return 0;
    }
}