using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Console;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.CLI.Verbs;

public class ListModlists
{
    private readonly ILogger<ListModlists> _logger;
    private readonly Client _client;
    private readonly IConsoleRenderer _console;

    public ListModlists(ILogger<ListModlists> logger, Client wjClient, IConsoleRenderer console)
    {
        _logger = logger;
        _client = wjClient;
        _console = console;
    }

    public static VerbDefinition Definition =
        new("list-modlists", "Lists all known modlists", Array.Empty<OptionDefinition>());

    public async Task<int> Run(CancellationToken token)
    {
        _console.Info("Loading all modlist definitions...");
        var modlists = await _client.LoadLists();

        _console.Table(
            $"Available Modlists ({modlists.Length})",
            modlists.OrderBy(l => l.NamespacedName),
            ("Name", m => m.NamespacedName),
            ("Game", m => m.Game.MetaData().HumanFriendlyGameName),
            ("Size", m => m.DownloadMetadata?.Size.FileSizeToString() ?? "Unknown"));

        return 0;
    }
}