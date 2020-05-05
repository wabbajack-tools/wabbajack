using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.Win32;
#nullable enable

namespace Wabbajack.Common.StoreHandlers
{
    public class SteamGame : AStoreGame
    {
        public override Game Game { get; internal set; }
        public override StoreType Type { get; internal set; } = StoreType.STEAM;

        public AbsolutePath Universe;

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

        public AbsolutePath SteamPath { get; set; }
        private AbsolutePath SteamConfig => new RelativePath("config//config.vdf").RelativeTo(SteamPath);
        private List<AbsolutePath>? SteamUniverses { get; set; }

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

                var steamPath = steamPathKey.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(steamPath))
                {
                    Utils.Error(new StoreException("Path to the Steam Directory from registry is Null or Empty!"));
                    return false;
                }

                SteamPath = new AbsolutePath(steamPath);

                if (!SteamPath.Exists)
                {
                    Utils.Error(new StoreException($"Path to the Steam Directory from registry does not exists: {SteamPath}"));
                    return false;
                }

                if (SteamConfig.Exists)
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

        private List<AbsolutePath> LoadUniverses()
        {
            var ret = new List<AbsolutePath>();

            SteamConfig.ReadAllLines().Do(l =>
            {
                if (!l.ContainsCaseInsensitive("BaseInstallFolder_")) return;
                var s = new AbsolutePath(GetVdfValue(l));
                var path = new RelativePath("steamapps").RelativeTo(s);

                if (!path.Exists)
                {
                    Utils.Log($"Directory {path} does not exist, skipping");
                    return;
                }

                ret.Add(path);
                Utils.Log($"Steam Library found at {path}");
            });

            Utils.Log($"Total number of Steam Libraries found: {ret.Count}");

            // Default path in the Steam folder isn't in the configs
            var defaultPath = new RelativePath("steamapps").RelativeTo(SteamPath);
            if(defaultPath.Exists)
                ret.Add(defaultPath);

            return ret;
        }

        public override bool LoadAllGames()
        {
            SteamUniverses ??= LoadUniverses();

            if (SteamUniverses.Count == 0)
            {
                Utils.Log("Could not find any Steam Libraries");
                return false;
            }

            SteamUniverses.Do(u =>
            {
                Utils.Log($"Searching for Steam Games in {u}");

                u.EnumerateFiles(false, "*.acf")
                    .Where(a => a.Exists)
                    .Where(a => a.IsFile)
                    .Do(f =>
                {
                    var game = new SteamGame();
                    var gotID = false;

                    f.ReadAllLines().Do(l =>
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

                        var path =  new RelativePath("common").Combine(GetVdfValue(l)).RelativeTo(u);
                        if (path.Exists)
                            game.Path = path;
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
            var workshop = new RelativePath("workshop").RelativeTo(game.Universe);
            if (!workshop.Exists)
                return;

            workshop.EnumerateFiles(false, "*.acf")
                .Where(f => f.Exists)
                .Where(f => f.IsFile)
                .Do(f =>
            {
                if (f.FileName.ToString() != $"appworkshop{game.ID}.acf")
                    return;

                Utils.Log($"Found Steam Workshop item file {f} for \"{game.Name}\"");

                var lines = f.ReadAllLines().ToList();
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

                    currentItem ??= new SteamWorkshopItem(game);

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
