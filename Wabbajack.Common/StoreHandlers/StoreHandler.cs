using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFinder;
using GameFinder.StoreHandlers.BethNet;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Origin;
using GameFinder.StoreHandlers.Steam;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Common.StoreHandlers
{
    public class StoreHandler
    {

        private static readonly Lazy<StoreHandler> _instance = new(() => new StoreHandler(), isThreadSafe: true);
        public static StoreHandler Instance => _instance.Value;

        private static readonly Lazy<SteamHandler> SteamHandler = new(() => new SteamHandler(new StoreHandlerLogger("Steam")));
        private static readonly Lazy<GOGHandler> GogHandler = new(() => new GOGHandler(new StoreHandlerLogger("GOG")));
        private static readonly Lazy<BethNetHandler> BethNetHandler = new(() => new BethNetHandler(new StoreHandlerLogger("BethNet")));
        private static readonly Lazy<EGSHandler> EpicGameStoreHandler = new(() => new EGSHandler(new StoreHandlerLogger("EGS")));
        private static readonly Lazy<OriginHandler> OriginHandler = new(() => new OriginHandler(true, true, new StoreHandlerLogger("Origin")));

        private readonly List<AStoreGame> _storeGames;
        public Dictionary<Game, AStoreGame> Games = new();
        
        private void FindGames<THandler, TGame>(Lazy<THandler> lazyHandler, string name) 
            where THandler : AStoreHandler<TGame> 
            where TGame : AStoreGame
        {
            try
            {
                var handler = lazyHandler.Value;
                var res = handler.FindAllGames();
                if (!res) return;
                
                foreach (var game in handler.Games)
                {
                    Utils.Log($"{handler.StoreType}: Found game {game} at \"{game.Path}\"");
                    _storeGames.Add(game);
                }
            }
            catch (Exception e)
            {
                Utils.Error(e, $"Could not load all Games from {name}");
            }
        }
        
        public StoreHandler()
        {
            _storeGames = new List<AStoreGame>();
            
            FindGames<SteamHandler, SteamGame>(SteamHandler, "SteamHandler");
            FindGames<GOGHandler, GOGGame>(GogHandler, "GOGHandler");
            FindGames<BethNetHandler, BethNetGame>(BethNetHandler, "BethNetHandler");
            FindGames<EGSHandler, EGSGame>(EpicGameStoreHandler, "EGSHandler");
            FindGames<OriginHandler, OriginGame>(OriginHandler, "OriginHandler");

            foreach (var storeGame in _storeGames)
            {
                IEnumerable<KeyValuePair<Game, GameMetaData>>? enumerable = storeGame switch
                {
                    SteamGame steamGame => GameRegistry.Games.Where(y => y.Value.SteamIDs?.Contains(steamGame.ID) ?? false),
                    GOGGame gogGame => GameRegistry.Games.Where(y => y.Value.GOGIDs?.Contains(gogGame.GameID) ?? false),
                    BethNetGame bethNetGame => GameRegistry.Games.Where(y => y.Value.BethNetID.Equals((int)bethNetGame.ID)),
                    EGSGame egsGame => GameRegistry.Games.Where(y => y.Value.EpicGameStoreIDs.Contains(egsGame.CatalogItemId ?? string.Empty)),
                    OriginGame originGame => GameRegistry.Games.Where(y => y.Value.OriginIDs.Contains(originGame.Id ?? string.Empty)),
                    _ => null
                };

                if (enumerable == null) continue;
                
                var list = enumerable.ToList();
                if (list.Count == 0) continue;

                var game = list.First().Key;
                if (Games.ContainsKey(game)) continue;
                
                Games.Add(game, storeGame);
            }
        }

        public AbsolutePath? TryGetGamePath(Game game)
        {
            if (!Games.TryGetValue(game, out var storeGame))
                return null;
            
            return (AbsolutePath)storeGame.Path;
        }

        public static void Warmup()
        {
            Task.Run(() => _instance.Value).FireAndForget();
        }
    }

    internal class StoreHandlerLogger : ILogger
    {
        private readonly string _name;
        
        public StoreHandlerLogger(string name)
        {
            _name = name;
        }
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Utils.Log($"{_name}: {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
