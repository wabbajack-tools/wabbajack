using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public enum Game 
    {
        //MO2 GAMES
        //Morrowind,
        Oblivion,
        [Description("Fallout 3")]
        Fallout3,
        [Description("Fallout New Vegas")]
        FalloutNewVegas,
        Skyrim,
        [Description("Skyrim Special Edition")]
        SkyrimSpecialEdition,
        [Description("Fallout 4")]
        Fallout4,
        [Description("Skyrim VR")]
        SkyrimVR,
        //VORTEX GAMES
        [Description("Darkest Dungeon")]
        DarkestDungeon,
        [Description("Divinity Original Sin 2")]
        DivinityOriginalSin2,
        [Description("Divinity Original Sin 2 Definitive Edition")]
        DivinityOriginalSin2DE, //definitive edition has its own nexus page but same Steam/GOG ids
        Starbound,
        [Description("Star Wars: Knights of the Old Republic")]
        SWKOTOR,
        [Description("Star Wars: Knights of the Old Republic 2")]
        SWKOTOR2,
        Witcher,
        [Description("Witcher 2")]
        Witcher2,
        [Description("Witcher 3")]
        Witcher3
    }

    public class GameMetaData
    {
        public ModManager SupportedModManager { get; internal set; }
        public string MO2ArchiveName { get; internal set; }
        public Game Game { get; internal set; }
        public string NexusName { get; internal set; }
        public string MO2Name { get; internal set; }
        public string GameLocationRegistryKey { get; internal set; }
        // to get steam ids: https://steamdb.info
        public List<int> SteamIDs { get; internal set; }
        // to get gog ids: https://www.gogdb.org
        public List<int> GOGIDs { get; internal set; }
        // these are additional folders when a game installs mods outside the game folder
        public List<string> AdditionalFolders { get; internal set; }
        // file to check if the game is present, useful when steamIds and gogIds dont help
        public List<string> RequiredFiles { get; internal set; }

        public string GameLocation
        {
            get
            {
                if (Consts.TestMode)
                    return Directory.GetCurrentDirectory();

                return (string) Registry.GetValue(GameLocationRegistryKey, "installed path", null)
                       ??
                       (string) Registry.GetValue(
                           GameLocationRegistryKey.Replace(@"HKEY_LOCAL_MACHINE\SOFTWARE\",
                               @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\"), "installed path", null);
            }
        }
    }

    public class GameRegistry
    {
        public static GameMetaData GetByMO2ArchiveName(string gameName)
        {
            var gamename = gameName.ToLower();
            return Games.Values.FirstOrDefault(g => g.MO2ArchiveName?.ToLower() == gamename);
        }

        public static GameMetaData GetByNexusName(string gameName)
        {
            return Games.Values.FirstOrDefault(g => g.NexusName == gameName.ToLower());
        }

        public static IReadOnlyDictionary<Game, GameMetaData> Games = new Dictionary<Game, GameMetaData>
        {
            /*{
                Game.Morrowind, new GameMetaData()
            },*/
            {
                Game.Oblivion, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Oblivion,
                    NexusName = "oblivion",
                    MO2Name = "Oblivion",
                    MO2ArchiveName = "oblivion",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Oblivion",
                    SteamIDs = new List<int> {22330},
                    RequiredFiles = new List<string>
                    {
                        "oblivion.exe"
                    }
                }
            },

            {
                Game.Fallout3, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout3,
                    NexusName = "fallout3",
                    MO2Name = "fallout3",
                    MO2ArchiveName = "fallout3",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Fallout3",
                    SteamIDs = new List<int> {22300, 22370}, // base game and GotY
                    RequiredFiles = new List<string>
                    {
                        "falloutlauncher.exe",
                        "data\\fallout3.esm"
                    }
                }
            },
            {
                Game.FalloutNewVegas, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.FalloutNewVegas,
                    NexusName = "newvegas",
                    MO2Name = "New Vegas",
                    MO2ArchiveName = "falloutnv",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\falloutnv",
                    SteamIDs = new List<int> {22380},
                    RequiredFiles = new List<string>
                    {
                        "FalloutNV.exe"
                    }
                }
            },
            {
                Game.Skyrim, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Skyrim,
                    NexusName = "skyrim",
                    MO2Name = "Skyrim",
                    MO2ArchiveName = "skyrim",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\skyrim",
                    SteamIDs = new List<int> {72850},
                    RequiredFiles = new List<string>
                    {
                        "tesv.exe"
                    }
                }
            },
            {
                Game.SkyrimSpecialEdition, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.SkyrimSpecialEdition,
                    NexusName = "skyrimspecialedition",
                    MO2Name = "Skyrim Special Edition",
                    MO2ArchiveName = "skyrimse",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Skyrim Special Edition",
                    SteamIDs = new List<int> {489830},
                    RequiredFiles = new List<string>
                    {
                        "SkyrimSE.exe"
                    }
                }
            },
            {
                Game.Fallout4, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout4,
                    NexusName = "fallout4",
                    MO2Name = "Fallout 4",
                    MO2ArchiveName = "fallout4",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Fallout4",
                    SteamIDs = new List<int> {377160},
                    RequiredFiles = new List<string>
                    {
                        "Fallout4.exe"
                    }
                }
            },
            /*{
                Game.Fallout4VR, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout4VR,
                    NexusName = "fallout4",
                    MO2Name = "Fallout 4",
                    MO2ArchiveName = "fallout4",
                    SteamIDs = new List<int>{611660}
                }
            },*/
            {
                Game.SkyrimVR, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.SkyrimVR,
                    NexusName = "skyrimspecialedition",
                    MO2Name = "Skyrim VR",
                    MO2ArchiveName = "skyrimse",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Skyrim VR",
                    SteamIDs = new List<int> {611670},
                    RequiredFiles = new List<string>
                    {
                        "SkyrimVR.exe"
                    }
                }
            },
            {
                Game.DarkestDungeon, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.DarkestDungeon,
                    NexusName = "darkestdungeon",
                    SteamIDs = new List<int> {262060},
                    GOGIDs = new List<int>{1450711444},
                    RequiredFiles = new List<string>
                    {
                        "_windows\\Darkest.exe"
                    }
                }
            },
            {
                Game.DivinityOriginalSin2DE, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.DivinityOriginalSin2DE,
                    NexusName = "divinityoriginalsin2definitiveedition",
                    SteamIDs = new List<int> {435150},
                    GOGIDs = new List<int>{1584823040},
                    AdditionalFolders = new List<string>
                    {
                        "%documents%\\Larian Studios\\Divinity Original Sin 2 Definitive Edition\\Mods\\"
                    },
                    RequiredFiles = new List<string>
                    {
                        "DefEd\\bin\\SuppportTool.exe"
                    }
                }
            },
            {
                Game.DivinityOriginalSin2, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.DivinityOriginalSin2,
                    NexusName = "divinityoriginalsin2",
                    SteamIDs = new List<int> {435150},
                    GOGIDs = new List<int>{1584823040},
                    AdditionalFolders = new List<string>
                    {
                        "%documents%\\Larian Studios\\Divinity Original Sin 2\\Mods\\",
                    },
                    RequiredFiles = new List<string>
                    {
                        "bin\\SuppportTool.exe"
                    }
                }
            },
            {
                Game.Starbound, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.Starbound,
                    NexusName = "starbound",
                    SteamIDs = new List<int>{211820},
                    GOGIDs = new List<int>{1452598881},
                    RequiredFiles = new List<string>
                    {
                        "win64\\starbound.exe"
                    }
                }
            },
            {
                Game.SWKOTOR, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.SWKOTOR,
                    NexusName = "kotor",
                    SteamIDs = new List<int>{32370},
                    GOGIDs = new List<int>{1207666283},
                    RequiredFiles = new List<string>
                    {
                        "swkotor.exe"
                    }
                }
            },
            {
                Game.SWKOTOR2, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.SWKOTOR2,
                    NexusName = "kotor2",
                    SteamIDs = new List<int>{208580},
                    GOGIDs = new List<int>{1421404581},
                    RequiredFiles = new List<string>
                    {
                        "swkotor2.exe"
                    }
                }
            },
            {
                Game.Witcher, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.Witcher,
                    NexusName = "witcher",
                    SteamIDs = new List<int>{20900},
                    GOGIDs = new List<int>{1207658924},
                    RequiredFiles = new List<string>
                    {
                        "system\\witcher.exe"
                    }
                }
            },
            {
                Game.Witcher2, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.Witcher2,
                    NexusName = "witcher2",
                    SteamIDs = new List<int>{20920},
                    GOGIDs = new List<int>{1207658930},
                    RequiredFiles = new List<string>
                    {
                        "bin\\witcher2.exe",
                        "bin\\userContentManager.exe"
                    }
                }
            },
            {
                Game.Witcher3, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.Witcher3,
                    NexusName = "witcher3",
                    SteamIDs = new List<int>{292030, 499450}, // normal and GotY
                    GOGIDs = new List<int>{1207664643, 1495134320, 1207664663, 1640424747}, // normal, GotY and both in packages
                    RequiredFiles = new List<string>
                    {
                        "bin\\x64\\witcher2.exe"
                    }
                }
            }
        };
    }
}
