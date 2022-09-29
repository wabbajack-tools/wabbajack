using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class ListGames : IVerb
{
    private readonly ILogger<ListGames> _logger;
    private readonly GameLocator _locator;

    public ListGames(ILogger<ListGames> logger, GameLocator locator)
    {
        _logger = logger;
        _locator = locator;
    }
    public Command MakeCommand()
    {
        var command = new Command("list-games");
        command.Description = "Lists all games Wabbajack recognizes, and their installed versions/locations (if any)";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }
    
    public async Task<int> Run(CancellationToken token)
    {
        foreach (var game in GameRegistry.Games.OrderBy(g => g.Value.HumanFriendlyGameName))
        {
            if (_locator.IsInstalled(game.Key))
            {
                var location = _locator.GameLocation(game.Key);
                var version = "unknown";
                var mainFile = game.Value.MainExecutable!.Value.RelativeTo(location);

                if (!mainFile.FileExists())
                    _logger.LogWarning("Main file {file} for {game} does not exist", mainFile, game.Key);

                var versionInfo = FileVersionInfo.GetVersionInfo(mainFile.ToString());
                
                _logger.LogInformation("[X] {Game} {Version} -> Path: {Path}", game.Value.HumanFriendlyGameName, versionInfo.ProductVersion ?? versionInfo.FileVersion, location);
            }
            else
            {
                _logger.LogInformation("[ ] {Game}", game.Value.HumanFriendlyGameName);
            }
        }

        return 0;
    }
}