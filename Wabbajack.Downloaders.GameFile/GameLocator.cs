using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Origin;
using GameFinder.StoreHandlers.Steam;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

public class GameLocator : IGameLocator
{
    private readonly EGSHandler? _egs;
    private readonly GOGHandler? _gog;
    private readonly Dictionary<Game, AbsolutePath> _locationCache;
    private readonly ILogger<GameLocator> _logger;
    private readonly OriginHandler? _origin;
    private readonly SteamHandler _steam;

    public GameLocator(ILogger<GameLocator> logger)
    {
        _logger = logger;
        _steam = new SteamHandler(logger);

        if (OperatingSystem.IsWindows())
        {
            _origin = new OriginHandler(true, false, logger);
            _gog = new GOGHandler(logger);

            _egs = new EGSHandler(logger);
        }

        _locationCache = new Dictionary<Game, AbsolutePath>();

        try
        {
            _steam.FindAllGames();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While finding Steam games");
        }

        try
        {
            _origin?.FindAllGames();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding Origin games");
        }

        try
        {
            _gog?.FindAllGames();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding GoG games");
        }

        try
        {
            _egs?.FindAllGames();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding Epic Games");
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

        try
        {
            foreach (var steamGame in _steam.Games.Where(steamGame => metaData.SteamIDs.Contains(steamGame.ID)))
            {
                path = steamGame!.Path.ToAbsolutePath();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding {Game} from Steam", game);
        }

        try
        {
            if (_gog != null)
            {
                foreach (var gogGame in _gog.Games.Where(gogGame => metaData.GOGIDs.Contains(gogGame.GameID)))
                {
                    path = gogGame!.Path.ToAbsolutePath();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding {Game} from GoG", game);
        }

        try
        {
            if (_egs != null)
            {
                foreach (var egsGame in _egs.Games.Where(egsGame =>
                             metaData.EpicGameStoreIDs.Contains(egsGame.CatalogItemId)))
                {
                    path = egsGame!.Path.ToAbsolutePath();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding {Game} from Epic", game);
        }


        try
        {
            if (_origin != null)
            {
                foreach (var originGame in _origin.Games.Where(originGame =>
                             metaData.EpicGameStoreIDs.Contains(originGame.Id)))
                {
                    path = originGame.Path.ToAbsolutePath();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "While finding {Game} from Origin", game);
        }

        path = default;
        return false;
    }
}