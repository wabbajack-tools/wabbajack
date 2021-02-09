using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Wabbajack.Common.StoreHandlers;

namespace Wabbajack.Common
{
    [JsonConverter(typeof(StringEnumConverter))]
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
        [Description("Fallout 4 VR")]
        Fallout4VR,
        //MO2 Non-BGS Games
        [Description("Darkest Dungeon")]
        DarkestDungeon,
        Dishonored,
        Witcher3,
        [Description("Stardew Valley")]
        StardewValley,
        KingdomComeDeliverance,
        MechWarrior5Mercenaries,
        NoMansSky,
        DragonAgeOrigins,
        DragonAge2,
        DragonAgeInquisition,
        [Description("Kerbal Space Program")]
        KerbalSpaceProgram
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

        public bool IsGenericMO2Plugin { get; internal set; }

        public string? MO2ArchiveName { get; internal set; }
        public string? NexusName { get; internal set; }
        // Nexus DB id for the game, used in some specific situations
        public long NexusGameId { get; internal set; }
        public string? MO2Name { get; internal set; }

        // to get steam ids: https://steamdb.info
        public List<int>? SteamIDs { get; internal set; }

        // to get gog ids: https://www.gogdb.org
        public List<int>? GOGIDs { get; internal set; }

        // to get these ids, split the numbers from the letters in file names found in
        // C:\ProgramData\Origin\LocalContent\{game name)\*.mfst
        // So for DA:O this is "DR208591800.mfst" -> "DR:208591800"
        public List<string> OriginIDs { get; set; } = new();

        public List<string> EpicGameStoreIDs { get; internal set; } = new List<string>();

        // to get BethNet IDs: check the registry
        public int BethNetID { get; internal set; }
        //for BethNet games only!
        public string RegString { get; internal set; } = string.Empty;

        // file to check if the game is present, useful when steamIds and gogIds dont help
        public List<string>? RequiredFiles { get; internal set; }

        public string? MainExecutable { get; internal set; }

        // Games that this game are commonly confused with, for example Skyrim SE vs Skyrim LE
        public Game[] CommonlyConfusedWith { get; set; } = Array.Empty<Game>();

        /// <summary>
        ///  Other games this game can pull source files from (if the game is installed on the user's machine)
        /// </summary>
        public Game[] CanSourceFrom { get; set; } = Array.Empty<Game>();

        public string HumanFriendlyGameName => Game.GetDescription();

        private AbsolutePath _cachedPath = default;

        public string InstalledVersion
        {
            get
            {
                if (!TryGetGameLocation(out var gameLoc))
                    throw new GameNotInstalledException(this);
                if (MainExecutable == null)
                    throw new NotImplementedException();

                var info = FileVersionInfo.GetVersionInfo((string)gameLoc.Combine(MainExecutable));
                var version =  info.ProductVersion;
                if (string.IsNullOrWhiteSpace(version))
                {
                    version =
                        $"{info.ProductMajorPart}.{info.ProductMinorPart}.{info.ProductBuildPart}.{info.ProductPrivatePart}";
                    return version;
                }

                return version;
            }
        }

        public bool IsInstalled => TryGetGameLocation() != null;

        public AbsolutePath? TryGetGameLocation()
        {
            return StoreHandler.Instance.TryGetGamePath(Game);
        }

        public bool TryGetGameLocation(out AbsolutePath path)
        {
            if (_cachedPath != default)
            {
                path = _cachedPath;
                return true;
            }

            var ret = TryGetGameLocation();
            if (ret != null)
            {
                _cachedPath = ret.Value;
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

        public static IEnumerable<T> GetAllItems<T>() where T : struct
        {
           return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }

    public class GameRegistry
    {
        public static GameMetaData? GetByMO2ArchiveName(string gameName)
        {
            gameName = gameName.ToLower();
            return Games.Values.FirstOrDefault(g => g.MO2ArchiveName?.ToLower() == gameName);
        }

        public static GameMetaData? GetByNexusName(string gameName)
        {
            return Games.Values.FirstOrDefault(g => g.NexusName == gameName.ToLower());
        }

        public static GameMetaData? GetBySteamID(int id)
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
            return TryGetByFuzzyName(someName) ?? throw new ArgumentNullException(nameof(someName), $"\"{someName}\" could not be translated to a game!");
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
                    MO2Name = "Fallout 3",
                    MO2ArchiveName = "fallout3",
                    SteamIDs = new List<int> {22300, 22370}, // base game and GotY
                    GOGIDs = new List<int>{1454315831}, // GotY edition
                    RequiredFiles = new List<string>
                    {
                        "Fallout3.exe"
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
                    CommonlyConfusedWith = new []{Game.Skyrim, Game.SkyrimVR},
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
                    MainExecutable = "Fallout4.exe",
                    CommonlyConfusedWith = new [] {Game.Fallout4VR},
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
                    CommonlyConfusedWith = new []{Game.Skyrim, Game.SkyrimSpecialEdition},
                    CanSourceFrom = new [] {Game.SkyrimSpecialEdition}
                }
            },
            {
                Game.Enderal, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Enderal,
                    NexusName = "enderal",
                    NexusGameId = 2736,
                    MO2Name = "Enderal",
                    MO2ArchiveName = "enderal",
                    SteamIDs = new List<int>{1027920, 933480},
                    RequiredFiles = new List<string>
                    {
                        "TESV.exe"
                    },
                    MainExecutable = "TESV.exe"
                }
            },
            {
                Game.Fallout4VR, new GameMetaData
                {
                    SupportedModManager = ModManager.MO2,
                    Game = Game.Fallout4VR,
                    NexusName = "fallout4",
                    MO2Name = "Fallout 4 VR",
                    MO2ArchiveName = "Fallout4",
                    SteamIDs = new List<int>{611660},
                    RequiredFiles = new List<string>
                    {
                        "Fallout4VR.exe"
                    },
                    MainExecutable = "Fallout4VR.exe",
                    CommonlyConfusedWith = new [] {Game.Fallout4},
                    CanSourceFrom = new [] {Game.Fallout4}
                }
            },
            {
                Game.DarkestDungeon, new GameMetaData
                {
                    Game = Game.DarkestDungeon,
                    NexusName = "darkestdungeon",
                    MO2Name = "Darkest Dungeon",
                    NexusGameId = 804,
                    SteamIDs = new List<int> {262060},
                    GOGIDs = new List<int>{1450711444},
                    EpicGameStoreIDs = new List<string> {"b4eecf70e3fe4e928b78df7855a3fc2d"},
                    IsGenericMO2Plugin = true,
                    RequiredFiles = new List<string>
                    {
                        "_windowsnosteam\\Darkest.exe"
                    },
                    MainExecutable = "_windowsnosteam\\Darkest.exe"
                }
            },
            {
                Game.Dishonored, new GameMetaData
                {
                    Game = Game.Dishonored,
                    NexusName = "dishonored",
                    MO2Name = "Dishonored",
                    NexusGameId = 802,
                    SteamIDs = new List<int> {205100},
                    GOGIDs = new List<int>{1701063787},
                    RequiredFiles = new List<string>
                    {
                        "Binaries\\Win32\\Dishonored.exe"
                    },
                    MainExecutable = @"Binaries\Win32\Dishonored.exe"
                }
            },
            {
                Game.Witcher3, new GameMetaData
                {
                    Game = Game.Witcher3,
                    NexusName = "witcher3",
                    NexusGameId = 952,
                    MO2Name = "The Witcher 3: Wild Hunt",
                    SteamIDs = new List<int>{292030, 499450}, // normal and GotY
                    GOGIDs = new List<int>{1207664643, 1495134320, 1207664663, 1640424747}, // normal, GotY and both in packages
                    RequiredFiles = new List<string>
                    {
                        "bin\\x64\\witcher3.exe"
                    },
                    MainExecutable = @"bin\x64\witcher3.exe"
                }
            },
            {
                Game.StardewValley, new GameMetaData
                {
                    Game = Game.StardewValley,
                    NexusName = "stardewvalley",
                    MO2Name = "Stardew Valley",
                    NexusGameId = 1303,
                    SteamIDs = new List<int>{413150},
                    GOGIDs = new List<int>{1453375253},
                    IsGenericMO2Plugin = true,
                    RequiredFiles = new List<string>
                    {
                        "Stardew Valley.exe"
                    },
                    MainExecutable = "Stardew Valley.exe"
                }
            },
            {
                Game.KingdomComeDeliverance, new GameMetaData
                {
                    Game = Game.KingdomComeDeliverance,
                    NexusName = "kingdomcomedeliverance",
                    MO2Name = "Kingdom Come: Deliverance",
                    MO2ArchiveName = "kingdomcomedeliverance",
                    NexusGameId = 2298,
                    SteamIDs = new List<int>{379430},
                    GOGIDs = new List<int>{1719198803},
                    IsGenericMO2Plugin = true,
                    RequiredFiles = new List<string>
                    {
                        @"bin\Win64\KingdomCome.exe"
                    },
                    MainExecutable = @"bin\Win64\KingdomCome.exe"
                }
            },
            {
                Game.MechWarrior5Mercenaries, new GameMetaData
                {
                    Game = Game.MechWarrior5Mercenaries,
                    NexusName = "mechwarrior5mercenaries",
                    MO2Name = "Mechwarrior 5: Mercenaries",
                    MO2ArchiveName = "mechwarrior5mercenaries",
                    NexusGameId = 3099,
                    EpicGameStoreIDs = new List<string> {"9fd39d8ac72946a2a10a887ce86e6c35"},
                    IsGenericMO2Plugin = true,
                    RequiredFiles = new List<string>
                    {
                        @"MW5Mercs\Binaries\Win64\MechWarrior-Win64-Shipping.exe"
                    },
                    MainExecutable = @"MW5Mercs\Binaries\Win64\MechWarrior-Win64-Shipping.exe"
                }
            },
            {
                Game.NoMansSky, new GameMetaData
                {
                    Game = Game.NoMansSky,
                    NexusName = "nomanssky",
                    NexusGameId = 1634,
                    MO2Name = "No Man's Sky",
                    SteamIDs = new List<int>{275850},
                    GOGIDs = new List<int>{1446213994},
                    RequiredFiles = new List<string>
                    {
                        @"Binaries\NMS.exe"
                    },
                    MainExecutable = @"Binaries\NMS.exe"
                }
            },
            {
                Game.DragonAgeOrigins, new GameMetaData
                {
                    Game = Game.DragonAgeOrigins,
                    NexusName = "dragonage",
                    NexusGameId = 140,
                    MO2Name = "Dragon Age: Origins",
                    SteamIDs = new List<int>{47810},
                    OriginIDs = new List<string>{"DR:169789300", "DR:208591800"},
                    GOGIDs = new List<int>{1949616134},
                    RequiredFiles = new List<string>
                    {
                        @"bin_ship\daorigins.exe"
                    },
                    MainExecutable = @"bin_ship\daorigins.exe"
                }
            },
            {
                Game.DragonAge2, new GameMetaData
                {
                    Game = Game.DragonAge2,
                    NexusName = "dragonage2",
                    NexusGameId = 141,
                    MO2Name = "Dragon Age 2", // Probably wrong
                    SteamIDs = new List<int>{1238040},
                    OriginIDs = new List<string>{"OFB-EAST:59474"},
                    RequiredFiles = new List<string>
                    {
                        @"bin_ship\DragonAge2.exe"
                    },
                    MainExecutable = @"bin_ship\DragonAge2.exe"
                }
            },
            {
                Game.DragonAgeInquisition, new GameMetaData
                {
                    Game = Game.DragonAgeInquisition,
                    NexusName = "dragonageinquisition",
                    NexusGameId = 728,
                    MO2Name = "Dragon Age: Inquisition", // Probably wrong
                    SteamIDs = new List<int>{1222690},
                    OriginIDs = new List<string>{"OFB-EAST:51937"},
                    RequiredFiles = new List<string>
                    {
                        @"DragonAgeInquisition.exe"
                    },
                    MainExecutable = @"DragonAgeInquisition.exe"
                }
            },
            {
                Game.KerbalSpaceProgram, new GameMetaData
                {
                    Game = Game.KerbalSpaceProgram,
                    NexusName = "kerbalspaceprogram",
                    MO2Name = "Kerbal Space Program",
                    NexusGameId = 272,
                    SteamIDs = new List<int>{220200},
                    GOGIDs = new List<int>{1429864849},
                    IsGenericMO2Plugin = true,
                    RequiredFiles = new List<string>
                    {
                        @"KSP_x64.exe"
                    },
                    MainExecutable = @"KSP_x64.exe"
                }
            }
        };

        public static Dictionary<long, Game> ByNexusID =
            Games.Values.Where(g => g.NexusGameId != 0)
                .GroupBy(g => g.NexusGameId)
                .Select(g => g.First())
                .ToDictionary(d => d.NexusGameId, d => d.Game);

    }
}

