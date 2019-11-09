using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public enum Game {
        //MO2 GAMES
        Morrowind,
        Oblivion,
        Fallout3,
        FalloutNewVegas,
        Skyrim,
        SkyrimSpecialEdition,
        Fallout4,
        SkyrimVR,
        //VORTEX GAMES
        DarkestDungeon,
        DivinityOriginalSins2
    }

    public class GameMetaData
    {
        public ModManager SupportedModManager { get; internal set; }
        public string MO2ArchiveName { get; internal set; }
        public Game Game { get; internal set; }
        public string NexusName { get; internal set; }
        public string MO2Name { get; internal set; }
        public string GameLocationRegistryKey { get; internal set; }
        public List<int> SteamIDs { get; internal set; }
        public int GOGID { get; internal set; }
        public List<string> AdditionalFolders { get; internal set; }

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


        public static Dictionary<Game, GameMetaData> Games = new Dictionary<Game, GameMetaData>
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
                    SteamIDs = new List<int>{22330}
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
                    SteamIDs = new List<int>{22300, 22370} // base game and GotY
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
                    SteamIDs = new List<int>{22380}
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
                    SteamIDs = new List<int>{72850}
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
                    SteamIDs = new List<int>{489830}
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
                    SteamIDs = new List<int>{377160}
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
                    SteamIDs = new List<int>{611670}
                }
            },
            {
                Game.DarkestDungeon, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.DarkestDungeon,
                    NexusName = "darkestdungeon",
                    SteamIDs = new List<int>{262060},
                    GOGID = 1450711444
                }
            },
            {
                Game.DivinityOriginalSins2, new GameMetaData
                {
                    SupportedModManager = ModManager.Vortex,
                    Game = Game.DivinityOriginalSins2,
                    NexusName = "divinityoriginalsins2",
                    SteamIDs = new List<int>{435150},
                    GOGID = 1584823040,
                    AdditionalFolders = new List<string>
                    {
                        "%documents%\\Larian Studios\\Divinity Original Sin 2\\Mods\\",
                        "%documents%\\Larian Studios\\Divinity Original Sin 2 Definitive Edition\\Mods\\"
                    }
                }
            }
        };
    }
}
