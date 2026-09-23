using System.Runtime.InteropServices;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.EADesktop;
using GameFinder.StoreHandlers.EADesktop.Crypto.Windows;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Origin;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
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
    private readonly EADesktopHandler? _eaDesktop;

    private readonly Dictionary<AppId, SteamGame> _steamGames = new();
    private readonly Dictionary<GOGGameId, AbsolutePath> _gogGames = new();
    private readonly Dictionary<EGSGameId, AbsolutePath> _egsGames = new();
    private readonly Dictionary<OriginGameId, AbsolutePath> _originGames = new();
    private readonly Dictionary<EADesktopGameId, AbsolutePath> _eaDesktopGames = new();
    
    private readonly Dictionary<Game, AbsolutePath> _locationCache;
    private readonly ILogger<GameLocator> _logger;

    public GameLocator(ILogger<GameLocator> logger)
    {
        _logger = logger;
        var fileSystem = NexusMods.Paths.FileSystem.Shared;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsRegistry = new WindowsRegistry();

            _steam = new SteamHandler(fileSystem, windowsRegistry);
            _gog = new GOGHandler(windowsRegistry, fileSystem);
            _egs = new EGSHandler(windowsRegistry, fileSystem);
            _origin = new OriginHandler(fileSystem);
            _eaDesktop = new EADesktopHandler(fileSystem, new HardwareInfoProvider());
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
            var games = _steam.FindAllGamesById(out var errors);

            foreach (var (id, game) in games)
            {
                try
                {
                    var path = (AbsolutePath)game.Path.GetFullPath();

                    if (!path.DirectoryExists())
                    {
                        _logger.LogError("Game does not exist: {Game}", game);
                        continue;
                    }

                    _steamGames[id] = game;
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

        try
        {
            FindStoreGames(_egs, _egsGames, game => (AbsolutePath)game.InstallLocation.GetFullPath());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "While finding games installed with the Epic Games Store");
        }

        try
        {
            FindStoreGames(_origin, _originGames, game => (AbsolutePath)game.InstallPath.GetFullPath());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "While finding games installed with Origin");
        }
        try
        {
            FindStoreGames(_eaDesktop, _eaDesktopGames, game => (AbsolutePath)game.BaseInstallPath.GetFullPath());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "While finding games installed with EADesktop");
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
            path = (AbsolutePath)found.Path.GetFullPath();
            return true;
        }

        foreach (var id in metaData.GOGIDs)
        {
            if (!_gogGames.TryGetValue(GOGGameId.From(id), out var found)) continue;
            path = found;
            return true;
        }

        foreach (var id in metaData.EpicGameStoreIDs)
        {
            if (!_egsGames.TryGetValue(EGSGameId.From(id), out var found)) continue;
            path = found;
            return true;
        }

        foreach (var id in metaData.OriginIDs)
        {
            if (!_originGames.TryGetValue(OriginGameId.From(id), out var found)) continue;
            path = found;
            return true;
        }
        
        foreach (var id in metaData.EADesktopIDs)
        {
            if (!_eaDesktopGames.TryGetValue(EADesktopGameId.From(id), out var found)) continue;
            path = found;
            return true;
        }

        path = default;
        return false;
    }
    /// <summary>
    ///     Read straight out of the local <c>appmanifest</c>, which records a manifest id per installed
    ///     depot: exactly the build sitting on this disk, whatever Steam publishes now. Shared depots are
    ///     left out - they belong to another app and carry no files of this game - and so is any depot whose
    ///     manifest id is zero, which is what a depot Steam has listed but not installed looks like.
    /// </summary>
    public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests)
    {
        foreach (var id in game.MetaData().SteamIDs)
        {
            if (!_steamGames.TryGetValue(AppId.From((uint) id), out var steamGame)) continue;

            var found = steamGame.AppManifest.InstalledDepots.Values
                .Where(d => d.ManifestId.Value != 0)
                .Select(d => new SteamManifest {Depot = d.DepotId.Value, Manifest = d.ManifestId.Value})
                .OrderBy(m => m.Depot)
                .ToArray();

            if (found.Length == 0) continue;

            manifests = found;
            return true;
        }

        manifests = Array.Empty<SteamManifest>();
        return false;
    }

    public bool TryGetSteamBuildId(Game game, out string buildId)
    {
        var metaData = game.MetaData();

        foreach (var id in metaData.SteamIDs)
        {
            if (!_steamGames.TryGetValue(AppId.From((uint)id), out var steamGame))
                continue;

            var value = steamGame.AppManifest.BuildId.ToString();

            if (string.IsNullOrWhiteSpace(value) || value == "0")
                continue;

            buildId = value;
            return true;
        }

        buildId = string.Empty;
        return false;
    }
}
