using System.Runtime.InteropServices;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Origin;
using GameFinder.StoreHandlers.Steam;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloaders.GameFile;

public class GameLocator : IGameLocator
{
    private readonly SteamHandler _steam;
    private readonly GOGHandler? _gog;
    private readonly EGSHandler? _egs;
    private readonly OriginHandler? _origin;

    private readonly Dictionary<int, AbsolutePath> _steamGames = new();
    private readonly Dictionary<long, AbsolutePath> _gogGames = new();
    private readonly Dictionary<string, AbsolutePath> _egsGames = new();
    private readonly Dictionary<string, AbsolutePath> _originGames = new();

    private readonly Dictionary<Game, AbsolutePath> _locationCache;
    private readonly ILogger<GameLocator> _logger;

    public GameLocator(ILogger<GameLocator> logger)
    {
        _logger = logger;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsRegistry = new WindowsRegistry();
            
            _steam = new SteamHandler(windowsRegistry);
            _gog = new GOGHandler(windowsRegistry);
            _egs = new EGSHandler(windowsRegistry);
            _origin = new OriginHandler();
        }
        else
        {
            _steam = new SteamHandler(null);
        }
        
        _locationCache = new Dictionary<Game, AbsolutePath>();
        
        FindAllGames();
    }

    private void FindAllGames()
    {
        FindStoreGames(_steam.FindAllGames(), _steamGames,
            steamGame => steamGame.Path,
            steamGame => steamGame.AppId);

        if (_gog is not null)
        {
            FindStoreGames(_gog.FindAllGames(), _gogGames,
                gogGame => gogGame.Path,
                gogGame => gogGame.Id);
        }

        if (_egs is not null)
        {
            FindStoreGames(_egs.FindAllGames(), _egsGames,
                egsGame => egsGame.InstallLocation,
                egsGame => egsGame.CatalogItemId);
        }

        if (_origin is not null)
        {
            FindStoreGames(_origin.FindAllGames(), _originGames,
                originGame => originGame.InstallPath,
                originGame => originGame.Id);
        }
    }

    private void FindStoreGames<TGame, TId>(
        IEnumerable<(TGame? game, string? error)> games,
        IDictionary<TId, AbsolutePath> paths,
        Func<TGame, string> getPath,
        Func<TGame, TId> getId)
        where TGame : class
    {
        foreach (var (game, error) in games)
        {
            if (game is not null)
            {
                var path = getPath(game).ToAbsolutePath();
                if (path.DirectoryExists())
                {
                    paths[getId(game)] = path;
                    _logger.LogDebug("Found Game {} at {}", game, path);
                }
                else
                {
                    _logger.LogError("Game {} does not exist at {}", game, path);
                }
            }
            else
            {
                _logger.LogError("{}", error);
            }
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
            if (!_steamGames.TryGetValue(id, out var found)) continue;
            path = found;
            return true;
        }
        
        foreach (var id in metaData.GOGIDs)
        {
            if (!_gogGames.TryGetValue(id, out var found)) continue;
            path = found;
            return true;
        }

        foreach (var id in metaData.EpicGameStoreIDs)
        {
            if (!_egsGames.TryGetValue(id, out var found)) continue;
            path = found;
            return true;
        }

        foreach (var id in metaData.OriginIDs)
        {
            if (!_originGames.TryGetValue(id, out var found)) continue;
            path = found;
            return true;
        }
        
        path = default;
        return false;
    }
}