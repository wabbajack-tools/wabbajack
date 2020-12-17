using System;
using System.Linq;
using Microsoft.Win32;

namespace Wabbajack.Common.StoreHandlers
{
    public class EpicGameStoreHandler : AStoreHandler
    {
        public override StoreType Type { get; internal set; }

        public string BaseRegKey = @"SOFTWARE\Epic Games\EOS";
        public override bool Init()
        {
            return true;
        }

        public override bool LoadAllGames()
        {
            using var eosKey = Registry.CurrentUser.OpenSubKey(BaseRegKey);
            if (eosKey == null)
            {
                Utils.Log("Epic Game Store is not installed");
                return false;
            }

            var name = eosKey.GetValue("ModSdkMetadataDir");
            if (name == null)
            {
                Utils.Log("Registry key entry does not exist for Epic Game store");
                return false;
            }

            var byID = GameRegistry.Games.SelectMany(g => g.Value.EpicGameStoreIDs
                    .Select(id => (id, g.Value.Game)))
                .GroupBy(t => t.id)
                .ToDictionary(t => t.Key, t => t.First().Game);

            foreach (var itm in ((AbsolutePath)(string)(name!)).EnumerateFiles(false, "*.item"))
            {
                var item = itm.FromJson<EpicGameItem>();
                Console.WriteLine($"Found Epic Game Store Game: {item.DisplayName} at {item.InstallLocation}");

                if (byID.TryGetValue(item.CatalogItemId, out var game))
                {
                    Games.Add(new EpicStoreGame(game, item));
                }

            }
            


            return true;
        }

        public class EpicStoreGame : AStoreGame
        {
            public EpicStoreGame(Game game, EpicGameItem item)
            {
                Type = StoreType.EpicGameStore;
                Game = game;
                Path = (AbsolutePath)item.InstallLocation;
                Name = game.MetaData().HumanFriendlyGameName;
                
            }

            public override Game Game { get; internal set; }
            public override StoreType Type { get; internal set; }
        }

        public class EpicGameItem
        {
            public string DisplayName { get; set; } = "";
            public string InstallationGuid { get; set; } = "";
            public string CatalogItemId { get; set; } = "";
            public string CatalogNamespace { get; set; } = "";
            public string InstallSessionId { get; set; } = "";
            public string InstallLocation { get; set; } = "";
        }
    }
}
