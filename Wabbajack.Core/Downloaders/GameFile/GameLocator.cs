using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.GameFinder.Common;
using Wabbajack.GameFinder.RegistryUtils;
using Wabbajack.GameFinder.StoreHandlers.Steam;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.StoreHandlers.GOG;

namespace Wabbajack.Downloaders.GameFile;

public class GameLocator : IGameLocator
{
    private readonly SteamHandler _steam;
    private readonly GOGHandler? _gog;

    private readonly Dictionary<AppId, AbsolutePath> _steamGames = new();
    private readonly Dictionary<GOGGameId, AbsolutePath> _gogGames = new();

    private readonly Dictionary<Game, AbsolutePath> _locationCache;
    private readonly ILogger<GameLocator> _logger;

    public GameLocator(ILogger<GameLocator> logger)
    {
        _logger = logger;
        var fileSystem = Wabbajack.GameFinder.Paths.FileSystem.Shared;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsRegistry = new WindowsRegistry();

            _steam = new SteamHandler(fileSystem, windowsRegistry);
            _gog = new GOGHandler(windowsRegistry, fileSystem);
        }
        else
        {
            _steam = new SteamHandler(fileSystem, null);
        }

        _locationCache = new Dictionary<Game, AbsolutePath>();

        FindAllGames();
    }

    private void FindAllGames()
    {
        try
        {
            FindStoreGames(_steam, _steamGames, game => (AbsolutePath)game.Path.GetFullPath());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "While finding games installed with Steam");
        }

        try
        {
            FindStoreGames(_gog, _gogGames, game => (AbsolutePath)game.Path.GetFullPath());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "While finding games installed with GOG Galaxy");
        }
    }

    private void FindStoreGames<TGame, TId>(
        AHandler<TGame, TId>? handler,
        Dictionary<TId, AbsolutePath> paths,
        Func<TGame, AbsolutePath> getPath)
        where TGame : class, IGame
        where TId : notnull
    {
        if (handler is null) return;

        var games = handler.FindAllGamesById(out var errors);

        foreach (var (id, game) in games)
        {
            try
            {
                var path = getPath(game);
                if (!path.DirectoryExists())
                {
                    _logger.LogError("Game does not exist: {Game}", game);
                    continue;
                }

                paths[id] = path;
                _logger.LogInformation("Found {Game}", game);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "While locating {Game}", game);
            }
        }

        foreach (var error in errors)
        {
            _logger.LogError("{Error}", error);
        }
    }

    public AbsolutePath GameLocation(Game game)
    {
        if (TryFindLocation(game, out var path))
            return path;
        throw new Exception($"Can't find game {game}");
    }

    public bool IsInstalled(Game game)
    {
        return TryFindLocation(game, out _);
    }

    public bool TryFindLocation(Game game, out AbsolutePath path)
    {
        lock (_locationCache)
        {
            if (_locationCache.TryGetValue(game, out path))
                return true;

            if (TryFindLocationInner(game, out path))
            {
                _locationCache.Add(game, path);
                return true;
            }
        }

        return false;
    }

    private bool TryFindLocationInner(Game game, out AbsolutePath path)
    {
        var metaData = game.MetaData();

        foreach (var id in metaData.SteamIDs)
        {
            if (!_steamGames.TryGetValue(AppId.From((uint)id), out var found)) continue;
            path = found;
            return true;
        }

        foreach (var id in metaData.GOGIDs)
        {
            if (!_gogGames.TryGetValue(GOGGameId.From(id), out var found)) continue;
            path = found;
            return true;
        }

        path = default;
        return false;
    }
}
