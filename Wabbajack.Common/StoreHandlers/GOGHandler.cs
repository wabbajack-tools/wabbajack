using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;

namespace Wabbajack.Common.StoreHandlers
{
    public class GOGGame : AStoreGame
    {
        public override Game Game { get; internal set; }
        public override string Name { get; internal set; }
        public override string Path { get; internal set; }
        public override int ID { get; internal set; }
        public override StoreType Type { get; internal set; } = StoreType.GOG;
    }

    public class GOGHandler : AStoreHandler
    {
        public override List<AStoreGame> Games { get; set; }
        public override StoreType Type { get; internal set; }

        private const string GOGRegKey = @"Software\GOG.com\Games";
        private const string GOG64RegKey = @"Software\WOW6432Node\GOG.com\Games";

        private RegistryKey GOGKey { get; set; }

        public override bool Init()
        {
            try
            {
                var gogKey = Registry.LocalMachine.OpenSubKey(GOGRegKey) ??
                             Registry.LocalMachine.OpenSubKey(GOG64RegKey);

                if (gogKey == null)
                {
                    Utils.Error(new StoreException("Could not open the GOG registry key!"));
                    return false;
                }

                GOGKey = gogKey;
                return true;
            }
            catch (SecurityException se)
            {
                Utils.Error(se, "GOGHandler could not read from registry!");
            }
            catch (UnauthorizedAccessException uae)
            {
                Utils.Error(uae, "GOGHandler could not read from registry!");
            }

            return false;
        }

        public override bool LoadAllGames()
        {
            try
            {
                string[] keys = GOGKey.GetSubKeyNames();
                Utils.Log($"Found {keys.Length} SubKeys for GOG");

                keys.Do(key =>
                {
                    if (!int.TryParse(key, out var gameID))
                    {
                        Utils.Error(new StoreException($"Could not read gameID for key {key}"));
                        return;
                    }

                    var subKey = GOGKey.OpenSubKey(key);
                    if (subKey == null)
                    {
                        Utils.Error(new StoreException($"Could not open SubKey for {key}"));
                        return;
                    }

                    var gameNameValue = subKey.GetValue("GAMENAME");
                    if (gameNameValue == null)
                    {
                        Utils.Error(new StoreException($"Could not get GAMENAME for {gameID} at {key}"));
                        return;
                    }

                    var gameName = gameNameValue.ToString();

                    var pathValue = subKey.GetValue("PATH");
                    if (pathValue == null)
                    {
                        Utils.Error(new StoreException($"Could not get PATH for {gameID} at {key}"));
                        return;
                    }

                    var path = pathValue.ToString();

                    var game = new GOGGame() {ID = gameID, Name = gameName, Path = path};

                    var gameMeta = GameRegistry.Games.Values.FirstOrDefault(g =>
                        g.GOGIDs.Contains(game.ID)
                        &&
                        g.RequiredFiles.TrueForAll(file =>
                            File.Exists(Path.Combine(game.Path, file))));

                    if (gameMeta == null)
                        return;

                    game.Game = gameMeta.Game;

                    Utils.Log($"Found GOG Game: \"{game.Name}\"({game.ID}) at {game.Path}");

                    Games.Add(game);
                });
            }
            catch (SecurityException se)
            {
                Utils.Error(se, "GOGHandler could not read from registry!");
            }
            catch (UnauthorizedAccessException uae)
            {
                Utils.Error(uae, "GOGHandler could not read from registry!");
            }
            catch (Exception e)
            {
                Utils.ErrorThrow(e);
            }

            Utils.Log($"Total number of GOG Games found: {Games.Count}");

            return true;
        }
    }
}
