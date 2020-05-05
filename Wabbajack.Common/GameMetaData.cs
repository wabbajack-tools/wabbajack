using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Wabbajack.Common.StoreHandlers;

namespace Wabbajack.Common
{
    public enum Game 
    {
        //MO2 GAMES
        Morrowind,
        Oblivion,
        [Description("Fallout 3")]
        Fallout3,
        [Description("Fallout New Vegas")]
        FalloutNewVegas,
        [Description("Skyrim Legendary Edition")]
        Skyrim,
        Enderal,
        [Description("Skyrim Special Edition")]
        SkyrimSpecialEdition,
        [Description("Fallout 4")]
        Fallout4,
        [Description("Skyrim VR")]
        SkyrimVR,
    }

    public static class GameExtensions
    {
        public static GameMetaData MetaData(this Game game)
        {
            return GameRegistry.Games[game];
        }
    }

    public class GameMetaData
    {
        public Game Game { get; internal set; }
        public ModManager SupportedModManager { get; internal set; }

        public string? MO2ArchiveName { get; internal set; }
        public string? NexusName { get; internal set; }
        // Nexus DB id for the game, used in some specific situations
        public long NexusGameId { get; internal set; }
        public string? MO2Name { get; internal set; }

        // to get steam ids: https://steamdb.info
        public List<int>? SteamIDs { get; internal set; }

        // to get gog ids: https://www.gogdb.org
        public List<int>? GOGIDs { get; internal set; }

        // to get BethNet IDs: check the registry
        public int BethNetID { get; internal set; }
        //for BethNet games only!
        public string RegString { get; internal set; } = string.Empty;

        // file to check if the game is present, useful when steamIds and gogIds dont help
        public List<string>? RequiredFiles { get; internal set; }

        public string? MainExecutable { get; internal set; }

        // Games that this game are commonly confused with, for example Skyrim SE vs Skyrim LE
        public Game[] CommonlyConfusedWith { get; set; } = Array.Empty<Game>();

        public string HumanFriendlyGameName => Game.GetDescription();

        public string InstalledVersion
        {
            get
            {
                if (!TryGetGameLocation(out var gameLoc))
                    throw new GameNotInstalledException(this);
                if (MainExecutable == null)
                    throw new NotImplementedException();

                return FileVersionInfo.GetVersionInfo((string)gameLoc.Combine(MainExecutable)).ProductVersion;
            }
        }

        public bool IsInstalled => TryGetGameLocation() != null;

        public AbsolutePath? TryGetGameLocation()
        {
            return StoreHandler.Instance.TryGetGamePath(Game);
        }

        public bool TryGetGameLocation(out AbsolutePath path)
        {
            var ret = TryGetGameLocation();
            if (ret != null)
            {
                path = ret.Value;
                return true;
            }

            path = default;
            return false;
        }

        public AbsolutePath GameLocation()
        {
            var ret = TryGetGameLocation();
            if (ret == null) throw new ArgumentNullException();
            return ret.Value;
        }
    }

    public class GameNotInstalledException : Exception
    {
        public GameNotInstalledException(GameMetaData gameMetaData) : base($"Game {gameMetaData.Game} does not appear to be installed.")
        {
        }
    }
    
    public static class EnumExtensions
    {
        public static string GetDescription<T>(this T enumerationValue)
            where T : Enum
        {
            var type = enumerationValue.GetType();
            if(!type.IsEnum)
            {
                throw new ArgumentException($"{nameof(enumerationValue)} must be of Enum type", nameof(enumerationValue));
            }
            var memberInfo = type.GetMember(enumerationValue.ToString()!);
            if (memberInfo.Length <= 0)
                return enumerationValue.ToString()!;

            var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attrs.Length > 0 ? ((DescriptionAttribute)attrs[0]).Description : enumerationValue.ToString();
        }
    }

    public class GameRegistry
    {
        public static GameMetaData GetByMO2ArchiveName(string gameName)
        {
            gameName = gameName.ToLower();
            return Games.Values.FirstOrDefault(g => g.MO2ArchiveName?.ToLower() == gameName);
        }

        public static GameMetaData GetByNexusName(string gameName)
        {
            return Games.Values.FirstOrDefault(g => g.NexusName == gameName.ToLower());
        }

        public static GameMetaData GetBySteamID(int id)
        {
            return Games.Values
                .FirstOrDefault(g => g.SteamIDs != null && g.SteamIDs.Count > 0 && g.SteamIDs.Any(i => i == id));
        }

        /// <summary>
        /// Parse game data from an arbitrary string. Tries first via parsing as a game Enum, then by Nexus name.
        /// <param nambe="someName">Name to query</param>
        /// <returns>GameMetaData found</returns>
        /// <exception cref="ArgumentNullException">If string could not be translated to a game</exception>
        /// </summary>
        public static GameMetaData GetByFuzzyName(string someName)
        {
            return TryGetByFuzzyName(someName) ?? throw new ArgumentNullException($"{someName} could not be translated to a game");
        }

        /// <summary>
        /// Tries to parse game data from an arbitrary string. Tries first via parsing as a game Enum, then by Nexus name.
        /// <param nambe="someName">Name to query</param>
        /// <returns>GameMetaData if found, otherwise null</returns>
        /// </summary>
        public static GameMetaData? TryGetByFuzzyName(string someName)
        {
            if (Enum.TryParse(typeof(Game), someName, true, out var metadata)) return ((Game)metadata!).MetaData();

            GameMetaData? result = GetByNexusName(someName);
            if (result != null) return result;

            result = GetByMO2ArchiveName(someName);
            if (result != null) return result;
            
            result = GetByMO2Name(someName);
            if (result != null) return result;


            return int.TryParse(someName, out int id) ? GetBySteamID(id) : null;
        }

        private static GameMetaData? GetByMO2Name(string gameName)
        {
            gameName = gameName.ToLower();
            return Games.Values.FirstOrDefault(g => g.MO2Name?.ToLower() == gameName);
        }

        public static bool TryGetByFuzzyName(string someName, [MaybeNullWhen(false)] out GameMetaData gameMetaData)
        {
            var result = TryGetByFuzzyName(someName);
            if (result == null)
            {
                gameMetaData = Games.Values.First();
                return false;
            }

            gameMetaData = result;
            return true;
        }

        public static IReadOnlyDictionary<Game, GameMetaData> Games = new Dictionary<Game, GameMetaData>
        {
            {
                Game.Morrowind, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Morrowind,
                    SteamIDs = new List<int>{22320},
                    GOGIDs = new List<int>{1440163901, 1435828767},
                    NexusName = "morrowind",
                    NexusGameId = 100,
                    MO2Name = "Morrowind",
                    MO2ArchiveName = "morrowind",
                    BethNetID = 31,
                    RegString = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\The Elder Scrolls III: Morrowind Game of the Year Edition",
                    RequiredFiles = new List<string>
                    {
                        "Morrowind.exe"
                    },
                    MainExecutable = "Morrowind.exe"
                }
            },
            {
                Game.Oblivion, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Oblivion,
                    NexusName = "oblivion",
                    NexusGameId = 101,
                    MO2Name = "Oblivion",
                    MO2ArchiveName = "oblivion",
                    SteamIDs = new List<int> {22330},
                    GOGIDs = new List<int>{1458058109},
                    RequiredFiles = new List<string>
                    {
                        "oblivion.exe"
                    },
                    MainExecutable = "Oblivion.exe"
                }
            },

            {
                Game.Fallout3, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout3,
                    NexusName = "fallout3",
                    NexusGameId = 120,
                    MO2Name = "fallout3",
                    MO2ArchiveName = "fallout3",
                    SteamIDs = new List<int> {22300, 22370}, // base game and GotY
                    RequiredFiles = new List<string>
                    {
                        "falloutlauncher.exe",
                        "data\\fallout3.esm"
                    },
                    MainExecutable = "Fallout3.exe"
                }
            },
            {
                Game.FalloutNewVegas, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.FalloutNewVegas,
                    NexusName = "newvegas",
                    NexusGameId = 130,
                    MO2Name = "New Vegas",
                    MO2ArchiveName = "falloutnv",
                    SteamIDs = new List<int> {22380, 22490}, // normal and RU version
                    GOGIDs = new List<int>{1454587428},
                    RequiredFiles = new List<string>
                    {
                        "FalloutNV.exe"
                    },
                    MainExecutable = "FalloutNV.exe"
                }
            },
            {
                Game.Skyrim, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Skyrim,
                    NexusName = "skyrim",
                    NexusGameId = 110,
                    MO2Name = "Skyrim",
                    MO2ArchiveName = "skyrim",
                    SteamIDs = new List<int> {72850},
                    RequiredFiles = new List<string>
                    {
                        "tesv.exe"
                    },
                    MainExecutable = "TESV.exe",
                    CommonlyConfusedWith = new [] {Game.SkyrimSpecialEdition, Game.SkyrimVR}
                }
            },
            {
                Game.SkyrimSpecialEdition, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.SkyrimSpecialEdition,
                    NexusName = "skyrimspecialedition",
                    NexusGameId = 1704,
                    MO2Name = "Skyrim Special Edition",
                    MO2ArchiveName = "skyrimse",
                    SteamIDs = new List<int> {489830},
                    RequiredFiles = new List<string>
                    {
                        "SkyrimSE.exe"
                    },
                    MainExecutable = "SkyrimSE.exe",
                    CommonlyConfusedWith = new []{Game.Skyrim, Game.SkyrimVR}
                }
            },
            {
                Game.Fallout4, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout4,
                    NexusName = "fallout4",
                    NexusGameId = 1151,
                    MO2Name = "Fallout 4",
                    MO2ArchiveName = "fallout4",
                    SteamIDs = new List<int> {377160},
                    RequiredFiles = new List<string>
                    {
                        "Fallout4.exe"
                    },
                    MainExecutable = "Fallout4.exe"
                }
            },
            {
                Game.SkyrimVR, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.SkyrimVR,
                    NexusName = "skyrimspecialedition",
                    NexusGameId = 1704,
                    MO2Name = "Skyrim VR",
                    MO2ArchiveName = "skyrimse",
                    SteamIDs = new List<int> {611670},
                    RequiredFiles = new List<string>
                    {
                        "SkyrimVR.exe"
                    },
                    MainExecutable = "SkyrimVR.exe",
                    CommonlyConfusedWith = new []{Game.Skyrim, Game.SkyrimSpecialEdition}
                }
            },
            {
                Game.Enderal, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Enderal,
                    NexusName = "enderal",
                    MO2Name = "Enderal",
                    MO2ArchiveName = "enderal",
                    SteamIDs = new List<int>{1027920, 933480},
                    RequiredFiles = new List<string>
                    {
                        "TESV.exe"
                    },
                    MainExecutable = "TESV.exe"
                }
            }
        };

    }
}
