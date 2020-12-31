using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.Win32;

#nullable enable

namespace Wabbajack.Common.StoreHandlers
{
    public class BethNetGame : AStoreGame
    {
        public override Game Game { get; internal set; }
        public override StoreType Type { get; internal set; } = StoreType.BethNet;
    }

    public class BethNetHandler : AStoreHandler
    {
        public override StoreType Type { get; internal set; } = StoreType.BethNet;

        private const string RegKey = @"SOFTWARE\WOW6432Node\bethesda softworks\Bethesda.net";

        public AbsolutePath BethPath { get; set; }
        private AbsolutePath Launcher => new RelativePath("BethesdaNetLauncher.exe").RelativeTo(BethPath);
        private AbsolutePath GamesFolder => new RelativePath("games").RelativeTo(BethPath);

        public override bool Init()
        {
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(RegKey);

                var pathKey = key?.GetValue("installLocation");
                if (pathKey == null)
                {
                    Utils.Error(new StoreException("Could not open the BethNetPath registry key!"));
                    return false;
                }

                var bethPath = pathKey.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(bethPath))
                {
                    Utils.Error(new StoreException("Path to the BethNet Directory from registry is Null or Empty!"));
                    return false;
                }

                BethPath = (AbsolutePath)bethPath;

                if (!BethPath.IsDirectory || !BethPath.Exists)
                {
                    Utils.Error(new StoreException($"Path to the BethNet Directory from registry does not exists: {BethPath}"));
                    return false;
                }

                if (Launcher.Exists && Launcher.IsFile)
                    return true;

                Utils.Error(new StoreException($"The BethNet Launcher could not be located: {Launcher}"));
                return false;
            }
            catch (SecurityException se)
            {
                Utils.Error(se, "BethNetHandler could not read from registry!");
            }
            catch (UnauthorizedAccessException uae)
            {
                Utils.Error(uae, "BethNetHandler could not read from registry!");
            }

            return false;
        }

        public override bool LoadAllGames()
        {
            List<BethNetGame> possibleGames = new List<BethNetGame>();
            // games folder

            if (!GamesFolder.Exists)
            {
                Utils.Error(new StoreException($"The GamesFolder for BethNet at {GamesFolder} does not exist!"));
                return false;
            }

            if (GamesFolder.Exists && GamesFolder.IsDirectory)
            {
                GamesFolder.EnumerateDirectories(false).Do(d =>
                {
                    var files = d.EnumerateFiles();
                    var game = GameRegistry.Games.Values
                        .FirstOrDefault(g => g.RequiredFiles?.All(f =>
                        {
                            var absPath = new RelativePath(f).RelativeTo(d);
                            return files.Contains(absPath);
                        }) ?? true);

                    if (game != null)
                    {
                        possibleGames.Add(new BethNetGame
                        {
                            Game = game.Game,
                            ID = game.BethNetID,
                            Name = game.Game.ToString(),
                            Path = d,
                            Type = StoreType.BethNet
                        });
                    }
                    else
                    {
                        Utils.Log($"BethNet Game at {d} is not supported!");
                    }
                });
            }
            
            possibleGames.Do(g =>
            {
                try
                {
                    var regString = g.Game.MetaData().RegString;
                    var regKey = Registry.LocalMachine.OpenSubKey(regString);
                    regString = @"HKEY_LOCAL_MACHINE\" + regString;
                    if (regKey == null)
                    {
                        Utils.Error(new StoreException($@"Could not open registry key at {regString}"));
                        return;
                    }

                    var pathValue = regKey.GetValue("Path");
                    var uninstallStringValue = regKey.GetValue("UninstallString");

                    if (pathValue == null || uninstallStringValue == null)
                    {
                        Utils.Error(new StoreException($@"Could not get Value from either {regString}\Path or UninstallString"));
                        return;
                    }

                    var path = pathValue.ToString() ?? string.Empty;
                    var uninstallString = uninstallStringValue.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(uninstallString))
                    {
                        Utils.Error(new StoreException($@"Path or UninstallString is null or empty for {regString}!"));
                        return;
                    }

                    path = FixRegistryValue(path);

                    if ((AbsolutePath)path != g.Path)
                    {
                        Utils.Error(new StoreException($"Path from registry does not equal game path: {path} != {g.Path} at {regString}"));
                        return;
                    }

                    var split = uninstallString.Split("\"");
                    if (split.Length != 3)
                    {
                        Utils.Error(new StoreException($"UninstallString at {regString} can not be split into 3 parts!"));
                        return;
                    }

                    var updaterPath = (AbsolutePath)split[1];
                    var args = split[2].Trim();

                    if (!updaterPath.Exists)
                    {
                        Utils.Error(new StoreException($"UpdaterPath from {regString} does not exist at {updaterPath}"));
                        return;
                    }

                    if (updaterPath.Parent != BethPath)
                    {
                        Utils.Error(new StoreException($"Parent of UpdatePath from {regString} is not BethPath: {updaterPath.Parent} != {BethPath}"));
                        return;
                    }

                    if (!args.Equals($"bethesdanet://uninstall/{g.ID}", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Utils.Error(new StoreException($"Uninstall arguments from {regString} is not valid: {args}"));
                        return;
                    }

                    Utils.Log($"Found BethNet game \"{g.Game}\" ({g.ID}) at {g.Path}");

                    Games.Add(g);
                }
                catch (SecurityException se)
                {
                    Utils.Error(se, "BethNetHandler could not read from registry!");
                }
                catch (UnauthorizedAccessException uae)
                {
                    Utils.Error(uae, "BethNetHandler could not read from registry!");
                }
            });

            Utils.Log($"Total number of BethNet Games found: {Games.Count}");

            return Games.Count != 0;
        }

        private static string FixRegistryValue(string value)
        {
            var s = value;
            if (s.StartsWith("\""))
                s = s[1..];
            if (s.EndsWith("\""))
                s = s[..^1];
            return s;
        }
    }
}
