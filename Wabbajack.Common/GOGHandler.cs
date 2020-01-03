using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class GOGGame
    {
        public int GameID;
        public string Path;
        public string GameName;
        public Game? Game;
    }

    /// <summary>
    /// Class for all GOG operations
    /// </summary>
    public class GOGHandler
    {
        private static readonly Lazy<GOGHandler> instance = new Lazy<GOGHandler>(
            () => new GOGHandler(true),
            isThreadSafe: true);
        public static GOGHandler Instance => instance.Value;

        private const string GOGRegKey = @"Software\GOG.com\Games";
        private const string GOG64RegKey = @"Software\WOW6432Node\GOG.com\Games";

        public HashSet<GOGGame> Games { get; internal set; }
        public RegistryKey GOGKey { get; internal set; }

        public GOGHandler(bool init)
        {
            var gogKey = Registry.LocalMachine.OpenSubKey(GOGRegKey) ?? Registry.LocalMachine.OpenSubKey(GOG64RegKey);
            if (gogKey == null)
            {
                Utils.ErrorThrow(new Exception("Could not find GOG in registry!"));
            }
            else
            {
                Utils.Log($"GOG registry key is ${gogKey}");
                GOGKey = gogKey;
                if (!init) return;
                LoadAllGames();
            }
        }

        /// <summary>
        /// Finds the installation path of a GOG game by ID
        /// </summary>
        /// <param name="id">ID of the GOG game</param>
        /// <returns></returns>
        public string GetGamePathById(int id)
        {
            return Games.FirstOrDefault(f => f.GameID == id)?.Path;
        }

        /// <summary>
        /// Enumerates through all subkeys found in the GOG registry entry to get all
        /// GOG games
        /// </summary>
        public void LoadAllGames()
        {
            Games = new HashSet<GOGGame>();
            if (GOGKey == null) return;
            string[] keys = GOGKey.GetSubKeyNames();
            Utils.Log($"Found {keys.Length} SubKeys for GOG");
            foreach (var key in keys)
            {
                if (!int.TryParse(key, out var gameID))
                {
                    Utils.ErrorThrow(new Exception($"Could not read gameID for key {key}"));
                }

                var subKey = GOGKey.OpenSubKey(key);
                if (subKey == null)
                {
                    Utils.ErrorThrow(new Exception($"Could not open SubKey for {key}"));
                    return;
                }

                var gameNameValue = subKey.GetValue("GAMENAME");
                if (gameNameValue == null)
                {
                    Utils.ErrorThrow(new Exception($"Could not get GAMENAME for {gameID} at {key}"));
                    return;
                }

                var gameName = gameNameValue.ToString();

                var pathValue = subKey.GetValue("PATH");
                if (pathValue == null)
                {
                    Utils.ErrorThrow(new Exception($"Could not get PATH for {gameID} at {key}"));
                    return;
                }

                var path = pathValue.ToString();


                var game = new GOGGame
                {
                    GameID = gameID,
                    GameName = gameName,
                    Path = path
                };

                Utils.Log($"Found GOG Game: {gameName}({gameID}) at {path}");

                game.Game = GameRegistry.Games.Values
                    .FirstOrDefault(g => g.GOGIDs != null && g.GOGIDs.Contains(game.GameID)
                      &&
                      g.RequiredFiles.TrueForAll(s => File.Exists(Path.Combine(game.Path, s))))?.Game;

                if (game.Game == null)
                {
                    Utils.Log("Found no matching game, continuing");
                }
                Games.Add(game);
            }
        }
    }
}
