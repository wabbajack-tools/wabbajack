using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Console;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class ListGames
{
    private readonly ILogger<ListGames> _logger;
    private readonly GameLocator _locator;
    private readonly IConsoleRenderer _console;

    public ListGames(ILogger<ListGames> logger, GameLocator locator, IConsoleRenderer console)
    {
        _logger = logger;
        _locator = locator;
        _console = console;
    }

    public static VerbDefinition Definition = new("list-games",
        "Lists all games Wabbajack recognizes, and their installed versions/locations (if any)", Array.Empty<OptionDefinition>());

    internal Task<int> Run(CancellationToken token)
    {
        var games = new List<GameInfo>();

        foreach (var game in GameRegistry.Games.OrderBy(g => g.Value.HumanFriendlyGameName))
        {
            if (_locator.IsInstalled(game.Key))
            {
                var location = _locator.GameLocation(game.Key);
                var mainFile = game.Value.MainExecutable!.Value.RelativeTo(location);

                string version = "Unknown";
                if (mainFile.FileExists())
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(mainFile.ToString());
                    version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "Unknown";
                }
                else
                {
                    _logger.LogWarning("Main file {file} for {game} does not exist", mainFile, game.Key);
                }

                games.Add(new GameInfo
                {
                    Name = game.Value.HumanFriendlyGameName,
                    Installed = true,
                    Version = version,
                    Path = location.ToString()
                });
            }
            else
            {
                games.Add(new GameInfo
                {
                    Name = game.Value.HumanFriendlyGameName,
                    Installed = false,
                    Version = "-",
                    Path = "-"
                });
            }
        }

        _console.Table(
            "Wabbajack Supported Games",
            games,
            ("Status", g => g.Installed ? "[green]Installed[/]" : "[grey]Not Found[/]"),
            ("Game", g => g.Name),
            ("Version", g => g.Version),
            ("Path", g => g.Installed ? g.Path : "-"));

        var installedCount = games.Count(g => g.Installed);
        _console.Info($"Found {installedCount} of {games.Count} supported games installed.");

        return Task.FromResult(0);
    }

    private class GameInfo
    {
        public string Name { get; init; } = "";
        public bool Installed { get; init; }
        public string Version { get; init; } = "";
        public string Path { get; init; } = "";
    }
}