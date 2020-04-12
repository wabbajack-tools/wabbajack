using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using DynamicData;
using Microsoft.Win32;
#nullable enable

namespace Wabbajack.Common.StoreHandlers
{
    public class SteamGame : AStoreGame
    {
        public override Game Game { get; internal set; }
        public override StoreType Type { get; internal set; } = StoreType.STEAM;

        public string Universe = string.Empty;

        public readonly List<SteamWorkshopItem> WorkshopItems = new List<SteamWorkshopItem>();
        public int WorkshopItemsSizeOnDisk;
    }

    public class SteamWorkshopItem
    {
        public readonly SteamGame Game;
        public int ItemID;
        public int Size;

        public SteamWorkshopItem(SteamGame game)
        {
            Game = game;
        }
    }

    public class SteamHandler : AStoreHandler
    {
        public override StoreType Type { get; internal set; } = StoreType.STEAM;

        private const string SteamRegKey = @"Software\Valve\Steam";

        public string SteamPath { get; set; } = string.Empty;
        private List<string>? SteamUniverses { get; set; }

        private string SteamConfig => Path.Combine(SteamPath, "config", "config.vdf");

        public override bool Init()
        {
            try
            {
                var steamKey = Registry.CurrentUser.OpenSubKey(SteamRegKey);

                var steamPathKey = steamKey?.GetValue("SteamPath");
                if (steamPathKey == null)
                {
                    Utils.Error(new StoreException("Could not open the SteamPath registry key!"));
                    return false;
                }

                SteamPath = steamPathKey.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(SteamPath))
                {
                    Utils.Error(new StoreException("Path to the Steam Directory from registry is Null or Empty!"));
                    return false;
                }

                if (!Directory.Exists(SteamPath))
                {
                    Utils.Error(new StoreException($"Path to the Steam Directory from registry does not exists: {SteamPath}"));
                    return false;
                }

                if (File.Exists(SteamConfig))
                    return true;

                Utils.Error(new StoreException($"The Steam config file could not be read: {SteamConfig}"));
                return false;

            }
            catch (SecurityException se)
            {
                Utils.Error(se, "SteamHandler could not read from registry!");
            }
            catch (UnauthorizedAccessException uae)
            {
                Utils.Error(uae, "SteamHandler could not read from registry!");
            }

            return false;
        }

        private List<string> LoadUniverses()
        {
            var ret = new List<string>();

            File.ReadAllLines(SteamConfig).Do(l =>
            {
                if (!l.ContainsCaseInsensitive("BaseInstallFolder_")) return;
                var s = GetVdfValue(l);
                s = Path.Combine(s, "steamapps");
                if (!Directory.Exists(s))
                {
                    Utils.Log($"Directory {s} does not exist, skipping");
                    return;
                }

                ret.Add(s);
                Utils.Log($"Steam Library found at {s}");
            });

            Utils.Log($"Total number of Steam Libraries found: {ret.Count}");

            // Default path in the Steam folder isn't in the configs
            if (Directory.Exists(Path.Combine(SteamPath, "steamapps")))
                ret.Add(Path.Combine(SteamPath, "steamapps"));

            return ret;
        }

        public override bool LoadAllGames()
        {
            if (SteamUniverses == null)
                SteamUniverses = LoadUniverses();

            if (SteamUniverses.Count == 0)
            {
                Utils.Log("Could not find any Steam Libraries");
                return false;
            }

            SteamUniverses.Do(u =>
            {
                Utils.Log($"Searching for Steam Games in {u}");

                Directory.EnumerateFiles(u, "*.acf", SearchOption.TopDirectoryOnly).Where(File.Exists).Do(f =>
                {
                    var game = new SteamGame();
                    var gotID = false;

                    File.ReadAllLines(f).Do(l =>
                    {
                        if (l.ContainsCaseInsensitive("\"appid\""))
                        {
                            if (!int.TryParse(GetVdfValue(l), out var id))
                                return;
                            game.ID = id;
                            gotID = true;
                        }

                        if (l.ContainsCaseInsensitive("\"name\""))
                            game.Name = GetVdfValue(l);

                        if (!l.ContainsCaseInsensitive("\"installdir\""))
                            return;

                        var path = Path.Combine(u, "common", GetVdfValue(l));
                        if (Directory.Exists(path))
                            game.Path = (AbsolutePath)path;
                    });

                    if (!gotID || !game.Path.IsDirectory) return;

                    var gameMeta = GameRegistry.Games.Values.FirstOrDefault(g =>
                    {
                        return (g.SteamIDs?.Contains(game.ID) ?? false)
                            && (g.RequiredFiles?.TrueForAll(file => game.Path.Combine(file).Exists) ?? true);
                    });

                    if (gameMeta == null)
                    {
                        Utils.Log($"Steam Game \"{game.Name}\" ({game.ID}) is not supported, skipping");
                        return;
                    }

                    game.Game = gameMeta.Game;
                    game.Universe = u;

                    Utils.Log($"Found Steam Game: \"{game.Name}\" ({game.ID}) at {game.Path}");

                    LoadWorkshopItems(game);

                    Games.Add(game);
                });
            });

            Utils.Log($"Total number of Steam Games found: {Games.Count}");

            return true;
        }

        private static void LoadWorkshopItems(SteamGame game)
        {
            var workshop = Path.Combine(game.Universe, "workshop");
            if (!Directory.Exists(workshop))
                return;

            Directory.EnumerateFiles(workshop, "*.acf", SearchOption.TopDirectoryOnly).Where(File.Exists).Do(f =>
            {
                if (Path.GetFileName(f) != $"appworkshop{game.ID}.acf")
                    return;

                Utils.Log($"Found Steam Workshop item file {f} for \"{game.Name}\"");

                var lines = File.ReadAllLines(f);
                var end = false;
                var foundAppID = false;
                var workshopItemsInstalled = 0;
                var workshopItemDetails = 0;
                var bracketStart = 0;
                var bracketEnd = 0;

                SteamWorkshopItem? currentItem = new SteamWorkshopItem(game);

                lines.Do(l =>
                {
                    if (end)
                        return;
                    if (currentItem == null)
                        currentItem = new SteamWorkshopItem(game);

                    var currentLine = lines.IndexOf(l);
                    if (l.ContainsCaseInsensitive("\"appid\"") && !foundAppID)
                    {
                        if (!int.TryParse(GetVdfValue(l), out var appID))
                            return;

                        foundAppID = true;

                        if (appID != game.ID)
                            return;
                    }

                    if (!foundAppID)
                        return;

                    if (l.ContainsCaseInsensitive("\"SizeOnDisk\""))
                    {
                        if (!int.TryParse(GetVdfValue(l), out var sizeOnDisk))
                            return;

                        game.WorkshopItemsSizeOnDisk += sizeOnDisk;
                    }

                    if (l.ContainsCaseInsensitive("\"WorkshopItemsInstalled\""))
                        workshopItemsInstalled = currentLine;

                    if (l.ContainsCaseInsensitive("\"WorkshopItemDetails\""))
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
                    game.WorkshopItems.Add(currentItem);

                    Utils.Log($"Found Steam Workshop item {currentItem.ItemID}");

                    currentItem = null;
                    end = true;
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
