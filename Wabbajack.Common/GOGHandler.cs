using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class GOGGame
    {
        public int GameID;
        public string Path;
        public string GameName;
    }

    /// <summary>
    /// Class for all GOG operations
    /// </summary>
    public class GOGHandler
    {
        private const string GOGRegKey = @"Software\GOG.com\Games";
        private const string GOG64RegKey = @"Software\WOW6432Node\GOG.com\Games";

        public HashSet<GOGGame> Games { get; internal set; }
        public RegistryKey GOGKey { get; internal set; }

        public GOGHandler(bool init)
        {
            var gogKey = Registry.LocalMachine.OpenSubKey(GOGRegKey) ?? Registry.LocalMachine.OpenSubKey(GOG64RegKey);
            GOGKey = gogKey;
            if (!init) return;
            LoadAllGames();
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
            string[] keys = GOGKey.GetSubKeyNames();
            foreach (var key in keys)
            {
                var game = new GOGGame
                {
                    GameID = int.Parse(key),
                    GameName = GOGKey.OpenSubKey(key)?.GetValue("GAMENAME").ToString(),
                    Path = GOGKey.OpenSubKey(key)?.GetValue("PATH").ToString()
                };

                Games.Add(game);
            }
        }
    }
}
