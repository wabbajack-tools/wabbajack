using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DynamicData;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class SteamGame
    {
        public int AppId;
        public string Name;
        public string InstallDir;
        public string BuildId;
        public Game? Game;

        public HashSet<SteamWorkshopItem> WorkshopItems;
        /// <summary>
        /// Combined size of all installed workshop items on disk in bytes
        /// </summary>
        public int WorkshopItemsSize;
    }

    public class SteamWorkshopItem
    {
        public SteamGame Game;
        public int ItemID;
        /// <summary>
        /// Size is in bytes
        /// </summary>
        public int Size;
    }

    /// <summary>
    /// Class for all Steam operations
    /// </summary>
    public class SteamHandler
    {
        private static readonly Lazy<SteamHandler> instance = new Lazy<SteamHandler>(
            () => new SteamHandler(true),
            isThreadSafe: true);
        public static SteamHandler Instance => instance.Value;

        private const string SteamRegKey = @"Software\Valve\Steam";

        /// <summary>
        /// Path to the Steam folder
        /// </summary>
        public string SteamPath { get; internal set; }
        /// <summary>
        /// HashSet of all known Steam Libraries
        /// </summary>
        public HashSet<string> InstallFolders { get; internal set; }
        /// <summary>
        /// HashSet of all known SteamGames
        /// </summary>
        public HashSet<SteamGame> Games { get; internal set; }

        private string SteamConfig => Path.Combine(SteamPath, "config", "config.vdf");

        public SteamHandler(bool init)
        {
            var steamKey = Registry.CurrentUser.OpenSubKey(SteamRegKey);
            SteamPath = steamKey?.GetValue("SteamPath").ToString();
            if(string.IsNullOrWhiteSpace(SteamPath) || steamKey == null || !Directory.Exists(SteamPath))
                Utils.ErrorThrow(new Exception("Could not find the Steam folder!"));
            if(!init) return;
            LoadInstallFolders();
            LoadAllSteamGames();
        }

        /// <summary>
        /// Finds the installation path of a Steam game by ID
        /// </summary>
        /// <param name="id">ID of the Steam game</param>
        /// <returns></returns>
        public string GetGamePathById(int id)
        {
            return Games.FirstOrDefault(f => f.AppId == id)?.InstallDir;
        }

        /// <summary>
        /// Reads the config file and adds all found installation folders to the HashSet
        /// </summary>
        public void LoadInstallFolders()
        {
            var paths = new HashSet<string>();

            File.ReadLines(SteamConfig, Encoding.UTF8).Do(l =>
            {
                if (!l.Contains("BaseInstallFolder_")) return;
                var s = GetVdfValue(l);
                s = Path.Combine(s, "steamapps");
                if (!Directory.Exists(s))
                    return;
                
                paths.Add(s);
                Utils.Log($"Steam Library found at {s}");
            });

            Utils.Log($"Total number of Steam Libraries found: {paths.Count}");

            // Default path in the Steam folder isn't in the configs
            if(Directory.Exists(Path.Combine(SteamPath, "steamapps")))
                paths.Add(Path.Combine(SteamPath, "steamapps"));

            InstallFolders = paths;
        }

        /// <summary>
        /// Enumerates through all Steam Libraries to find and read all .afc files, adding the found game
        /// to the HashSet
        /// </summary>
        public void LoadAllSteamGames()
        {
            var games = new HashSet<SteamGame>();

            InstallFolders.Do(p =>
            {
                Directory.EnumerateFiles(p, "*.acf", SearchOption.TopDirectoryOnly).Where(File.Exists).Do(f =>
                {
                    var steamGame = new SteamGame();
                    var valid = false;
                    File.ReadAllLines(f, Encoding.UTF8).Do(l =>
                    {
                        if(l.Contains("\"appid\""))
                            if (!int.TryParse(GetVdfValue(l), out steamGame.AppId))
                                return;
                        if(l.Contains("\"name\""))
                            steamGame.Name = GetVdfValue(l);
                        if (l.Contains("\"buildid\""))
                            steamGame.BuildId = GetVdfValue(l);
                        if (l.Contains("\"installdir\""))
                        {
                            var path = Path.Combine(p, "common", GetVdfValue(l));
                            steamGame.InstallDir = Directory.Exists(path) ? path : null;
                        }

                        if (steamGame.AppId != 0 && !string.IsNullOrWhiteSpace(steamGame.Name) &&
                            !string.IsNullOrWhiteSpace(steamGame.InstallDir))
                            valid = true;
                    });

                    if (!valid)
                        return;

                    steamGame.Game = GameRegistry.Games.Values
                        .FirstOrDefault(g => 
                            g.SteamIDs.Contains(steamGame.AppId)
                            &&
                            g.RequiredFiles.TrueForAll(s => File.Exists(Path.Combine(steamGame.InstallDir, s)))
                            )?.Game;
                    games.Add(steamGame);

                    Utils.Log($"Found Game: {steamGame.Name} ({steamGame.AppId}) at {steamGame.InstallDir}");
                });
            });

            Utils.Log($"Total number of Steam Games found: {games.Count}");

            Games = games;
        }

        public void LoadWorkshopItems(SteamGame game)
        {
            if(game.WorkshopItems == null)
                game.WorkshopItems = new HashSet<SteamWorkshopItem>();

            InstallFolders.Do(p =>
            {
                var workshop = Path.Combine(p, "workshop");
                if(!Directory.Exists(workshop))
                    return;

                Directory.EnumerateFiles(workshop, "*.acf", SearchOption.TopDirectoryOnly).Where(File.Exists).Do(f =>
                {
                    if (Path.GetFileName(f)  != $"appworkshop_{game.AppId}.acf")
                        return;

                    var foundAppID = false;
                    var workshopItemsInstalled = 0;
                    var workshopItemDetails = 0;
                    var currentItem = new SteamWorkshopItem();
                    var bracketStart = 0;
                    var bracketEnd = 0;
                    var lines = File.ReadAllLines(f, Encoding.UTF8);
                    var end = false;
                    lines.Do(l =>
                    {
                        if (end)
                            return;
                        if(currentItem == null)
                            currentItem = new SteamWorkshopItem();

                        var currentLine = lines.IndexOf(l);
                        if (l.Contains("\"appid\"") && !foundAppID)
                        {
                            if (!int.TryParse(GetVdfValue(l), out var appID))
                                return;

                            foundAppID = true;

                            if (appID != game.AppId)
                                return;
                        }

                        if (!foundAppID)
                            return;

                        if (l.Contains("\"SizeOnDisk\""))
                        {
                            if (!int.TryParse(GetVdfValue(l), out var sizeOnDisk))
                                return;

                            game.WorkshopItemsSize = sizeOnDisk;
                        }

                        if (l.Contains("\"WorkshopItemsInstalled\""))
                            workshopItemsInstalled = currentLine;

                        if (l.Contains("\"WorkshopItemDetails\""))
                            workshopItemDetails = currentLine;

                        if (workshopItemsInstalled == 0)
                            return;

                        if (currentLine <= workshopItemsInstalled + 1 && currentLine >= workshopItemDetails - 1)
                            return;

                        if (currentItem.ItemID == 0)
                            if (!int.TryParse(GetSingleVdfValue(l), out currentItem.ItemID))
                                return;

                        if (currentItem.ItemID == 0)
                            return;

                        if (bracketStart == 0 && l.Contains("{"))
                            bracketStart = currentLine;

                        if (bracketEnd == 0 && l.Contains("}"))
                            bracketEnd = currentLine;

                        if (bracketStart == 0)
                            return;

                        if (currentLine == bracketStart + 1)
                            if (!int.TryParse(GetVdfValue(l), out currentItem.Size))
                                return;

                        if (bracketStart == 0 || bracketEnd == 0 || currentItem.ItemID == 0 || currentItem.Size == 0)
                            return;

                        bracketStart = 0;
                        bracketEnd = 0;
                        currentItem.Game = game;
                        game.WorkshopItems.Add(currentItem);
                        currentItem = null;
                        end = true;
                    });
                });
            });
        }

        private static string GetVdfValue(string line)
        {
            var trim = line.Trim('\t').Replace("\t", "");
            string[] s = trim.Split('\"');
            return s[3].Replace("\\\\", "\\");
        }

        private static string GetSingleVdfValue(string line)
        {
            var trim = line.Trim('\t').Replace("\t", "");
            return trim.Split('\"')[1];
        }
    }
}
