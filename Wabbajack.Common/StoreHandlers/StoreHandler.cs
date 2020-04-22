using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.Common.StoreHandlers
{
    public enum StoreType
    {
        STEAM,
        GOG,
        BethNet
    }

    public class StoreHandler
    {
        private static readonly Lazy<StoreHandler> instance = new Lazy<StoreHandler>(() => new StoreHandler(), true);
        public static StoreHandler Instance => instance.Value;

        private static readonly Lazy<SteamHandler> _steamHandler = new Lazy<SteamHandler>(() => new SteamHandler());
        public SteamHandler SteamHandler = _steamHandler.Value;

        private static readonly Lazy<GOGHandler> _gogHandler = new Lazy<GOGHandler>(() => new GOGHandler());
        public GOGHandler GOGHandler = _gogHandler.Value;

        private static readonly Lazy<BethNetHandler> _bethNetHandler = new Lazy<BethNetHandler>(() => new BethNetHandler());
        public BethNetHandler BethNetHandler = _bethNetHandler.Value;

        public List<AStoreGame> StoreGames;

        public StoreHandler()
        {
            StoreGames = new List<AStoreGame>();

            if (SteamHandler.Init())
            {
                if(SteamHandler.LoadAllGames())
                    StoreGames.AddRange(SteamHandler.Games);
                else
                    Utils.Error(new StoreException("Could not load all Games from the SteamHandler, check previous error messages!"));
            }
            else
            {
                Utils.Error(new StoreException("Could not Init the SteamHandler, check previous error messages!"));
            }

            if (GOGHandler.Init())
            {
                if(GOGHandler.LoadAllGames())
                    StoreGames.AddRange(GOGHandler.Games);
                else
                    Utils.Error(new StoreException("Could not load all Games from the GOGHandler, check previous error messages!"));
            }
            else
            {
                Utils.Error(new StoreException("Could not Init the GOGHandler, check previous error messages!"));
            }

            if (BethNetHandler.Init())
            {
                if (BethNetHandler.LoadAllGames())
                    StoreGames.AddRange(BethNetHandler.Games);
                else
                    Utils.Error(new StoreException("Could not load all Games from the BethNetHandler, check previous error messages!"));
            }
            else
            {
                Utils.Error(new StoreException("Could not Init the BethNetHandler, check previous error messages!"));
            }
        }

        public AbsolutePath? TryGetGamePath(Game game)
        {
            return StoreGames.FirstOrDefault(g => g.Game == game)?.Path;
        }
    }

    public abstract class AStoreGame
    {
        public abstract Game Game { get; internal set; }
        public virtual string Name { get; internal set; } = string.Empty;
        public virtual AbsolutePath Path { get; internal set; }
        public virtual int ID { get; internal set; }
        public abstract StoreType Type { get; internal set; }
    }

    public abstract class AStoreHandler
    {
        public List<AStoreGame> Games { get; } = new List<AStoreGame>();

        public abstract StoreType Type { get; internal set; }

        public abstract bool Init();

        public abstract bool LoadAllGames();
    }

    public class StoreException : Exception
    {
        public StoreException(string msg) : base(msg)
        {

        }
    }
}
