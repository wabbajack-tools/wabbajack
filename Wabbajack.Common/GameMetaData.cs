using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public enum Game {
        Oblivion,
        Fallout3,
        FalloutNewVegas,
        Skyrim,
        SkyrimSpecialEdition,
        Fallout4
    }

    public class GameMetaData
    {
        public string MO2ArchiveName { get; internal set; }
        public Game Game { get; internal set; }
        public string NexusName { get; internal set; }
        public string MO2Name { get; internal set; }
        public string GameLocationRegistryKey { get; internal set; }

        public string GameLocation =>
            (string)Registry.GetValue(GameLocationRegistryKey, "installed path", null)
            ??
            (string)Registry.GetValue(GameLocationRegistryKey.Replace(@"HKEY_LOCAL_MACHINE\SOFTWARE\", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432"), "installed path", null);
    }

    public class GameRegistry
    {
        public static GameMetaData GetByMO2ArchiveName(string gameName)
        {
            var gamename = gameName.ToLower();
            return Games.Values.FirstOrDefault(g => g.MO2ArchiveName == gamename);
        }


        public static Dictionary<Game, GameMetaData> Games = new Dictionary<Game, GameMetaData>
        {
            {
                Game.Oblivion, new GameMetaData
                {
                    Game = Game.Oblivion,
                    NexusName = "oblivion",
                    MO2Name = "oblivion",
                    MO2ArchiveName = "oblivion",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Oblivion"
                }
            },

            {
                Game.Fallout3, new GameMetaData
                {
                    Game = Game.Fallout3,
                    NexusName = "fallout3",
                    MO2Name = "fallout3",
                    MO2ArchiveName = "fallout3",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Fallout3"
                }
            },
            {
                Game.FalloutNewVegas, new GameMetaData
                {
                    Game = Game.FalloutNewVegas,
                    NexusName = "newvegas",
                    MO2Name = "New Vegas",
                    MO2ArchiveName = "falloutnv",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\falloutnv"
                }
            },
            {
                Game.Skyrim, new GameMetaData
                {
                    Game = Game.Skyrim,
                    NexusName = "skyrim",
                    MO2Name = "Skyrim",
                    MO2ArchiveName = "skyrim",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\skyrim"
                }
            },
            {
                Game.SkyrimSpecialEdition, new GameMetaData
                {
                    Game = Game.SkyrimSpecialEdition,
                    NexusName = "skyrimspecialedition",
                    MO2Name = "Skyrim Special Edition",
                    MO2ArchiveName = "skyrimse",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Skyrim Special Edition"
                }
            },
            {
                Game.Fallout4, new GameMetaData
                {
                    Game = Game.Fallout4,
                    NexusName = "fallout4",
                    MO2Name = "Fallout 4",
                    MO2ArchiveName = "fallout4",
                    GameLocationRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\Fallout4"
                }
            }
        };
    }
}
