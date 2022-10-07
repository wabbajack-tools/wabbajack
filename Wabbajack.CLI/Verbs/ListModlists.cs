using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Bethesda;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.CLI.Verbs;

public class ListModlists : IVerb
{
    private readonly ILogger<ListCreationClubContent> _logger;
    private readonly Client _client;
    private readonly BethesdaDownloader _downloader;

    public ListModlists(ILogger<ListCreationClubContent> logger, Client wjClient)
    {
        _logger = logger;
        _client = wjClient;
    }

    public static VerbDefinition Definition =
        new("list-modlists", "Lists all known modlists", Array.Empty<OptionDefinition>());

    public async Task<int> Run(CancellationToken token)
    {
        _logger.LogInformation("Loading all modlist definitions");
        var modlists = await _client.LoadLists();
        _logger.LogInformation("Loaded {Count} lists", modlists.Length);

        foreach (var modlist in modlists.OrderBy(l => l.NamespacedName))
        {
            _logger.LogInformation("{Url} {Game} {Size}", modlist.NamespacedName, modlist.Game.MetaData().HumanFriendlyGameName, modlist.DownloadMetadata!.Size.FileSizeToString());
        }
        
        return 0;
    }
}