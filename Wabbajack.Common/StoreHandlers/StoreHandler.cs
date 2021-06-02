using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFinder;
using GameFinder.StoreHandlers.BethNet;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Steam;

namespace Wabbajack.Common.StoreHandlers
{
    public class StoreHandler
    {
        private static readonly Lazy<StoreHandler> _instance = new(() => new StoreHandler(), isThreadSafe: true);
        public static StoreHandler Instance => _instance.Value;

        private static readonly Lazy<SteamHandler> _steamHandler = new(() => new SteamHandler());
        public SteamHandler SteamHandler = _steamHandler.Value;

        private static readonly Lazy<GOGHandler> _gogHandler = new(() => new GOGHandler());
        public GOGHandler GOGHandler = _gogHandler.Value;

        private static readonly Lazy<BethNetHandler> _bethNetHandler = new(() => new BethNetHandler());
        public BethNetHandler BethNetHandler = _bethNetHandler.Value;
        
        private static readonly Lazy<EGSHandler> _epicGameStoreHandler = new(() => new EGSHandler());
        public EGSHandler EpicGameStoreHandler = _epicGameStoreHandler.Value;
        
        private static readonly Lazy<OriginHandler> _originHandler = new(() => new OriginHandler());
        public OriginHandler OriginHandler = _originHandler.Value;

        private List<AStoreGame> _storeGames;

        public Dictionary<Game, AStoreGame> Games = new();
        
        private void FindGames<THandler, TGame>(Lazy<THandler> lazyHandler, string name) 
            where THandler : AStoreHandler<TGame> 
            where TGame : AStoreGame
        {
            try
            {
                var handler = lazyHandler.Value;
                var res = handler.FindAllGames();
                
                if (res.HasErrors)
                {
                    Utils.Error($"Errors while finding Games from {name}\n{res.ErrorsToString()}");
                }
                
                foreach (var game in handler.Games)
                {
                    Utils.Log($"{handler.StoreType}: Found game {game}");
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
            
            FindGames<SteamHandler, SteamGame>(_steamHandler, "SteamHandler");
            FindGames<GOGHandler, GOGGame>(_gogHandler, "GOGHandler");
            FindGames<BethNetHandler, BethNetGame>(_bethNetHandler, "BethNetHandler");
            FindGames<EGSHandler, EGSGame>(_epicGameStoreHandler, "EGSHandler");
            
            if (OriginHandler.Init())
            {
                if (!OriginHandler.LoadAllGames())
                    Utils.Error(new StoreException("Could not load all Games from the OriginHandler, check previous error messages!"));
            }
            else
            {
                Utils.Error(new StoreException("Could not Init the OriginHandler, check previous error messages!"));
            }

            foreach (var storeGame in _storeGames)
            {
                IEnumerable<KeyValuePair<Game, GameMetaData>>? enumerable = storeGame switch
                {
                    SteamGame steamGame => GameRegistry.Games.Where(y => y.Value.SteamIDs?.Contains(steamGame.ID) ?? false),
                    GOGGame gogGame => GameRegistry.Games.Where(y => y.Value.GOGIDs?.Contains(gogGame.GameID) ?? false),
                    BethNetGame bethNetGame => GameRegistry.Games.Where(y => y.Value.BethNetID.Equals((int)bethNetGame.ID)),
                    EGSGame egsGame => GameRegistry.Games.Where(y => y.Value.EpicGameStoreIDs.Contains(egsGame.CatalogItemId ?? string.Empty)),
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
            if (Games.TryGetValue(game, out var storeGame))
                return (AbsolutePath) storeGame.Path;
            return OriginHandler.Games.FirstOrDefault(x => x.Game == game)?.Path;
        }

        public static void Warmup()
        {
            Task.Run(() => _instance.Value).FireAndForget();
        }
    }
    
    public class StoreException : Exception
    {
        public StoreException(string msg) : base(msg)
        {

        }
    }
}
