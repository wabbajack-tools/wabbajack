using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Paths;

namespace Wabbajack.DTOs;

public static class GameRegistry
{
    public static IReadOnlyDictionary<Game, GameMetaData> Games = new Dictionary<Game, GameMetaData>
    {
        {
            Game.Morrowind, new GameMetaData
            {
                Game = Game.Morrowind,
                SteamIDs = new[] {22320},
                GOGIDs = new long[] {1440163901, 1435828767},
                NexusName = "morrowind",
                NexusGameId = 100,
                MO2Name = "Morrowind",
                MO2ArchiveName = "morrowind",
                BethNetID = 31,
                RegString =
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\The Elder Scrolls III: Morrowind Game of the Year Edition",
                RequiredFiles = new[]
                {
                    "Morrowind.exe".ToRelativePath()
                },
                MainExecutable = "Morrowind.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/661c1c090ff5831a647202397c61d73c/24/32x32.png"
            }
        },
        {
            Game.Oblivion, new GameMetaData
            {
                Game = Game.Oblivion,
                NexusName = "oblivion",
                NexusGameId = 101,
                MO2Name = "Oblivion",
                MO2ArchiveName = "oblivion",
                SteamIDs = new[] {22330},
                GOGIDs = new long[] {1458058109},
                RequiredFiles = new[]
                {
                    "oblivion.exe".ToRelativePath()
                },
                MainExecutable = "Oblivion.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/e403262769f74b83009bffb6e3c0a3b7/32/32x32.png"
            }
        },

        {
            Game.Fallout3, new GameMetaData
            {
                Game = Game.Fallout3,
                NexusName = "fallout3",
                NexusGameId = 120,
                MO2Name = "Fallout 3",
                MO2ArchiveName = "fallout3",
                SteamIDs = new[] {22300, 22370}, // base game and GotY
                GOGIDs = new long[] {1454315831}, // GotY edition
                RequiredFiles = new[]
                {
                    "Fallout3.exe".ToRelativePath()
                },
                MainExecutable = "Fallout3.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/ac7ed855f313b05391de74046180fb34.png"
            }
        },
        {
            Game.FalloutNewVegas, new GameMetaData
            {
                Game = Game.FalloutNewVegas,
                NexusName = "newvegas",
                NexusGameId = 130,
                MO2Name = "New Vegas",
                MO2ArchiveName = "falloutnv",
                SteamIDs = new[] {22380, 22490}, // normal and RU version
                GOGIDs = new long[] {1454587428},
                EpicGameStoreIDs = new[] {"dabb52e328834da7bbe99691e374cb84"},
                RequiredFiles = new[]
                {
                    "FalloutNV.exe".ToRelativePath()
                },
                MainExecutable = "FalloutNV.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/c706723a17a2b2acec4f9ebc9f572e31.png"
            }
        },
        {
            Game.Skyrim, new GameMetaData
            {
                Game = Game.Skyrim,
                NexusName = "skyrim",
                NexusGameId = 110,
                MO2Name = "Skyrim",
                MO2ArchiveName = "skyrim",
                SteamIDs = new[] {72850},
                RequiredFiles = new[]
                {
                    "tesv.exe".ToRelativePath()
                },
                MainExecutable = "TESV.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.SkyrimSpecialEdition, Game.SkyrimVR},
                IconSource = "https://cdn2.steamgriddb.com/icon/58ee2794cc87707943624dc8db2ff5a0/8/32x32.png"
            }
        },
        {
            Game.SkyrimSpecialEdition, new GameMetaData
            {
                Game = Game.SkyrimSpecialEdition,
                NexusName = "skyrimspecialedition",
                NexusGameId = 1704,
                MO2Name = "Skyrim Special Edition",
                MO2ArchiveName = "skyrimse",
                SteamIDs = new[] {489830},
                GOGIDs = new long[]
                {
                    1711230643,// The Elder Scrolls V: Skyrim Special Edition AKA Base Game
                    1801825368,// The Elder Scrolls V: Skyrim Anniversary Edition AKA The Store Bundle 
                    1162721350 // Upgrade DLC
                },
                RequiredFiles = new[]
                {
                    "SkyrimSE.exe".ToRelativePath()
                },
                MainExecutable = "SkyrimSE.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.Skyrim, Game.SkyrimVR},
                IconSource = "https://cdn2.steamgriddb.com/icon/e1b90346c92331860b1391257a106bb1/32/32x32.png"
            }
        },
        {
            Game.Fallout4, new GameMetaData
            {
                Game = Game.Fallout4,
                NexusName = "fallout4",
                NexusGameId = 1151,
                MO2Name = "Fallout 4",
                MO2ArchiveName = "fallout4",
                SteamIDs = new[] {377160},
                GOGIDs = new long[]{1998527297},
                RequiredFiles = new[]
                {
                    "Fallout4.exe".ToRelativePath()
                },
                MainExecutable = "Fallout4.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.Fallout4VR},
                IconSource = "https://cdn2.steamgriddb.com/icon/578d9dd532e0be0cdd050b5bec4967a1.png"
            }
        },
        {
            Game.SkyrimVR, new GameMetaData
            {
                Game = Game.SkyrimVR,
                NexusName = "skyrimspecialedition",
                NexusGameId = 1704,
                MO2Name = "Skyrim VR",
                MO2ArchiveName = "skyrimse",
                SteamIDs = new[] {611670},
                RequiredFiles = new[]
                {
                    "SkyrimVR.exe".ToRelativePath()
                },
                MainExecutable = "SkyrimVR.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.Skyrim, Game.SkyrimSpecialEdition},
                CanSourceFrom = new[] {Game.SkyrimSpecialEdition},
                IconSource = "https://cdn2.steamgriddb.com/icon/75b3f26dde5a6c2a415464b05bd46fbc.png"
            }
        },
        {
            Game.Enderal, new GameMetaData
            {
                Game = Game.Enderal,
                NexusName = "enderal",
                NexusGameId = 2736,
                MO2Name = "Enderal",
                MO2ArchiveName = "enderal",
                SteamIDs = new[] {1027920, 933480},
                RequiredFiles = new[]
                {
                    "TESV.exe".ToRelativePath()
                },
                MainExecutable = "TESV.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.EnderalSpecialEdition},
                IconSource = "https://cdn2.steamgriddb.com/icon/6505e8a0c0e1a90d8da8879e49a437f0.png"
            }
        },
        {
            Game.EnderalSpecialEdition, new GameMetaData
            {
                Game = Game.EnderalSpecialEdition,
                NexusName = "enderalspecialedition",
                NexusGameId = 3685,
                MO2Name = "Enderal Special Edition",
                MO2ArchiveName = "enderalse",
                SteamIDs = new[] {976620},
                GOGIDs = new long[] {1708684988},
                RequiredFiles = new[]
                {
                    "SkyrimSE.exe".ToRelativePath()
                },
                MainExecutable = "SkyrimSE.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.Enderal},
                IconSource = "https://cdn2.steamgriddb.com/icon/104c6f99020b85465ae361a92d09a8d1.png"
            }
        },
        {
            Game.Fallout4VR, new GameMetaData
            {
                Game = Game.Fallout4VR,
                NexusName = "fallout4",
                MO2Name = "Fallout 4 VR",
                MO2ArchiveName = "Fallout4",
                SteamIDs = new[] {611660},
                RequiredFiles = new[]
                {
                    "Fallout4VR.exe".ToRelativePath()
                },
                MainExecutable = "Fallout4VR.exe".ToRelativePath(),
                CommonlyConfusedWith = new[] {Game.Fallout4},
                CanSourceFrom = new[] {Game.Fallout4},
                IconSource = "https://cdn2.steamgriddb.com/icon/9058c666789874c718d1976270cee814.png"
            }
        },
        {
            Game.DarkestDungeon, new GameMetaData
            {
                Game = Game.DarkestDungeon,
                NexusName = "darkestdungeon",
                MO2Name = "Darkest Dungeon",
                NexusGameId = 804,
                SteamIDs = new[] {262060},
                GOGIDs = new long[] {1450711444},
                EpicGameStoreIDs = new[] {"b4eecf70e3fe4e928b78df7855a3fc2d"},
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"_windowsnosteam\Darkest.exe".ToRelativePath()
                },
                MainExecutable = @"_windowsnosteam\Darkest.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/b1d2128cee734a257c5e0d5c73bbdd1b.png"
            }
        },
        {
            Game.Dishonored, new GameMetaData
            {
                Game = Game.Dishonored,
                NexusName = "dishonored",
                MO2Name = "Dishonored",
                MO2ArchiveName = "dishonored",
                NexusGameId = 802,
                SteamIDs = new[] {205100},
                GOGIDs = new long[] {1701063787},
                RequiredFiles = new[]
                {
                    @"Binaries\Win32\Dishonored.exe".ToRelativePath()
                },
                MainExecutable = @"Binaries\Win32\Dishonored.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/6fcd734d28ae00944f8f7c68a219bbc5/32/32x32.png"
            }
        },
        {
            Game.Witcher, new GameMetaData
            {
                Game = Game.Witcher,
                NexusName = "witcher",
                NexusGameId = 150,
                MO2Name = "The Witcher: Enhanced Edition",
                MO2ArchiveName = "witcher",
                SteamIDs = new[] {20900}, // normal and GotY
                GOGIDs = new long[] {1207658924}, // normal, GotY and both in packages
                RequiredFiles = new[]
                {
                    @"System\witcher.exe".ToRelativePath()
                },
                MainExecutable = @"System\witcher.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/fd72ecaa23aa0a514a53c6a16eabb9c6.png"
            }
        },
        {
            Game.Witcher3, new GameMetaData
            {
                Game = Game.Witcher3,
                NexusName = "witcher3",
                NexusGameId = 952,
                MO2Name = "The Witcher 3: Wild Hunt",
                MO2ArchiveName = "witcher3",
                SteamIDs = new[] {292030, 499450}, // normal and GotY
                GOGIDs = new long[]
                    {1207664643, 1495134320, 1207664663, 1640424747}, // normal, GotY and both in packages
                RequiredFiles = new[]
                {
                    @"bin\x64\witcher3.exe".ToRelativePath()
                },
                MainExecutable = @"bin\x64\witcher3.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/2af9b1a840b4ecd522fe1cda88c8385e/32/32x32.png"
            }
        },
        {
            Game.StardewValley, new GameMetaData
            {
                Game = Game.StardewValley,
                NexusName = "stardewvalley",
                MO2Name = "Stardew Valley",
                MO2ArchiveName = "stardewvalley",
                NexusGameId = 1303,
                SteamIDs = new[] {413150},
                GOGIDs = new long[] {1453375253},
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    "Stardew Valley.exe".ToRelativePath()
                },
                MainExecutable = "Stardew Valley.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/f6c4718557e1197ecdbe1b7ff52975d2.png"
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
                SteamIDs = new[] {379430},
                GOGIDs = new long[] {1719198803},
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"bin\Win64\KingdomCome.exe".ToRelativePath()
                },
                MainExecutable = @"bin\Win64\KingdomCome.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/1bdde90ebfdef547440410e79b1877bf.png"
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
                EpicGameStoreIDs = new[] {"9fd39d8ac72946a2a10a887ce86e6c35"},
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"MW5Mercs\Binaries\Win64\MechWarrior-Win64-Shipping.exe".ToRelativePath()
                },
                MainExecutable = @"MW5Mercs\Binaries\Win64\MechWarrior-Win64-Shipping.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/c59bb6bab3096620efe78bdeb031f027/8/32x32.png"
            }
        },
        {
            Game.NoMansSky, new GameMetaData
            {
                Game = Game.NoMansSky,
                NexusName = "nomanssky",
                NexusGameId = 1634,
                MO2Name = "No Man's Sky",
                SteamIDs = new[] {275850},
                GOGIDs = new long[] {1446213994},
                RequiredFiles = new[]
                {
                    @"Binaries\NMS.exe".ToRelativePath()
                },
                MainExecutable = @"Binaries\NMS.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/970e789e0a92eab99bcabf36dfa6050c/32/32x32.png"
            }
        },
        {
            Game.DragonAgeOrigins, new GameMetaData
            {
                Game = Game.DragonAgeOrigins,
                NexusName = "dragonage",
                NexusGameId = 140,
                MO2Name = "Dragon Age: Origins",
                SteamIDs = new[] {47810},
                OriginIDs = new[] {"DR:169789300", "DR:208591800"},
                EADesktopIDs = new [] // Possibly Wrong
                {
                    "9df89a8e-b201-4507-8a8d-bd6799fedb18",
                    "Origin.SFT.50.0000078", 
                    "Origin.SFT.50.0000078",
                    "Origin.SFT.50.0000078",
                    "Origin.SFT.50.0000085",
                    "Origin.SFT.50.0000086",
                    "Origin.SFT.50.0000087",
                    "Origin.SFT.50.0000088",
                    "Origin.SFT.50.0000089",
                    "Origin.SFT.50.0000090",
                    "Origin.SFT.50.0000091",
                    "Origin.SFT.50.0000097",
                    "Origin.SFT.50.0000098"
                },
                GOGIDs = new long[] {1949616134},
                RequiredFiles = new[]
                {
                    @"bin_ship\daorigins.exe".ToRelativePath()
                },
                MainExecutable = @"bin_ship\daorigins.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/b55d7ce2adb9449fc4dae6115cbbe30f/32/32x32.png"
            }
        },
        {
            Game.DragonAge2, new GameMetaData
            {
                Game = Game.DragonAge2,
                NexusName = "dragonage2",
                NexusGameId = 141,
                MO2Name = "Dragon Age 2", // Probably wrong
                SteamIDs = new[] {1238040},
                OriginIDs = new[] {"OFB-EAST:59474", "DR:201797000"},
                EADesktopIDs = new [] // Possibly Wrong
                {
                    "Origin.SFT.50.0000073",
                    "Origin.SFT.50.0000255",
                    "Origin.SFT.50.0000256",
                    "Origin.SFT.50.0000257",
                    "Origin.SFT.50.0000288",
                    "Origin.SFT.50.0000310",
                    "Origin.SFT.50.0000311",
                    "Origin.SFT.50.0000356",
                    "Origin.SFT.50.0000385",
                    "Origin.SFT.50.0000429",
                    "Origin.SFT.50.0000449",
                    "Origin.SFT.50.0000452",
                    "Origin.SFT.50.0000453"
                },
                RequiredFiles = new[]
                {
                    @"bin_ship\DragonAge2.exe".ToRelativePath()
                },
                MainExecutable = @"bin_ship\DragonAge2.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/a6a946f7265ed7f28a6425ee76621c3a/32/32x32.png"
            }
        },
        {
            Game.DragonAgeInquisition, new GameMetaData
            {
                Game = Game.DragonAgeInquisition,
                NexusName = "dragonageinquisition",
                NexusGameId = 728,
                MO2Name = "Dragon Age: Inquisition", // Probably wrong
                SteamIDs = new[] {1222690},
                OriginIDs = new[] {"OFB-EAST:51937", "OFB-EAST:1000032"},
                RequiredFiles = new[]
                {
                    @"DragonAgeInquisition.exe".ToRelativePath()
                },
                MainExecutable = @"DragonAgeInquisition.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/b98004311446c60521a8831075423c20.png"
            }
        },
        {
            Game.KerbalSpaceProgram, new GameMetaData
            {
                Game = Game.KerbalSpaceProgram,
                NexusName = "kerbalspaceprogram",
                MO2Name = "Kerbal Space Program",
                NexusGameId = 272,
                SteamIDs = new[] {220200},
                GOGIDs = new long[] {1429864849},
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"KSP_x64.exe".ToRelativePath()
                },
                MainExecutable = @"KSP_x64.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/2ee4162f4a89db5fa43b3b08900ee370.png"
            }
        },
        {
            Game.Terraria, new GameMetaData
            {
                Game = Game.Terraria,
                SteamIDs = new[] {1281930},
                MO2Name = "Terraria",
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"tModLoader.exe".ToRelativePath()
                },
                MainExecutable = @"tModLoader.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/e658047c67a80c47b5ba982ab520b59a.png"
            }
        },
        {
           Game.Cyberpunk2077, new GameMetaData
           {
                Game = Game.Cyberpunk2077,
                SteamIDs = new[] {1091500},
                GOGIDs = new long[] {2093619782, 1423049311},
                EpicGameStoreIDs = new[] {"5beededaad9743df90e8f07d92df153f"},
                MO2Name = "Cyberpunk 2077",
                NexusName = "cyberpunk2077",
                NexusGameId = 3333,
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"bin\x64\Cyberpunk2077.exe".ToRelativePath()
                },
                MainExecutable = @"bin\x64\Cyberpunk2077.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/2d45da15db966ba887cf4e573989fcc8/32/32x32.png"
            }
        },
        {
           Game.Sims4, new GameMetaData
           {
                Game = Game.Sims4,
                SteamIDs = new[] {1222670},
                MO2Name = "The Sims 4",
                NexusName = "thesims4",
                NexusGameId = 641,
                IsGenericMO2Plugin = true,
                RequiredFiles = new[]
                {
                    @"Game\Bin\TS4_x64.exe".ToRelativePath()
                },
                MainExecutable = @"Game\Bin\TS4_x64.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/9fc664916bce863561527f06a96f5ff3/32/32x32.png"
            }
        },
        {
            Game.DragonsDogma, new GameMetaData
            {
                Game = Game.DragonsDogma,
                SteamIDs = new[] {367500 },
                GOGIDs = new long[]{1242384383},
                MO2Name = "Dragon's Dogma: Dark Arisen",
                MO2ArchiveName = "dragonsdogma",
                NexusName = "dragonsdogma",
                NexusGameId = 1249,
                IsGenericMO2Plugin = true,
                RequiredFiles = new []
                {
                    @"DDDA.exe".ToRelativePath()
                },
                MainExecutable = @"DDDA.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/a830839bbb4a4022a84ff2b8af5c46e0.png"
            }
        },
        {
            Game.KarrynsPrison, new GameMetaData
            {
                Game = Game.KarrynsPrison,
                SteamIDs = new[] { 1619750 },
                MO2Name = "Karryn's Prison",
                MO2ArchiveName = "karrynsprison",
                IsGenericMO2Plugin = false,
                RequiredFiles = new []
                {
                    "nw.exe".ToRelativePath()
                },
                MainExecutable = "nw.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/37286bc401299e97a564f6b42792eb6d.png"
            }
        },
        {
            Game.Valheim, new GameMetaData
            {
                Game = Game.Valheim,
                SteamIDs = new[] { 892970 },
                MO2Name = "Valheim",
                MO2ArchiveName = "valheim",
                NexusName = "valheim",
                NexusGameId = 3667,
                IsGenericMO2Plugin = true,
                RequiredFiles = new []
                {
                    "valheim.exe".ToRelativePath()
                },
                MainExecutable = "valheim.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/dd055f53a45702fe05e449c30ac80df9/32/32x32.png"
            }
        },
        {
            Game.MountAndBlade2Bannerlord, new GameMetaData
            {
                Game = Game.MountAndBlade2Bannerlord,
                NexusName = "mountandblade2bannerlord",
                NexusGameId = 3174,
                MO2Name = "Mount & Blade II: Bannerlord",
                MO2ArchiveName = "mountandblade2bannerlord",
                SteamIDs = new[] { 261550 },
                GOGIDs = new long[] {
                    1564781494, //Mount & Blade II: Bannerlord : Game
                    1681929523, //Mount & Blade II: Bannerlord - Digital Deluxe : Package
                    1802539526, //Mount & Blade II: Bannerlord : Package
                },
                IsGenericMO2Plugin = true,
                RequiredFiles = new []
                {
                    @"bin\Win64_Shipping_Client\Bannerlord.exe".ToRelativePath() 
                },
                MainExecutable = @"bin\Win64_Shipping_Client\Bannerlord.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/811cf46d61c9ae564bf7fa4b5abc639b.png"
            }
        },
        {
            Game.FinalFantasy7Remake, new GameMetaData
            {
                Game = Game.FinalFantasy7Remake,
                NexusName = "finalfantasy7remake",
                NexusGameId = 4202,
                MO2Name = "FINAL FANTASY VII REMAKE INTERGRADE",
                MO2ArchiveName = "finalfantasy7remake",
                SteamIDs = new[] { 1462040 },
                IsGenericMO2Plugin = true,
                RequiredFiles = new []
                {
                    @"End\Binaries\Win64\ff7remake_.exe".ToRelativePath(),
                    @"ff7remake_.exe".ToRelativePath()
                },
                MainExecutable = @"End\Binaries\Win64\ff7remake_.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/d9b47f916e531ac9ef2b0887ca72d698.png"
            }
        },
        {
            Game.BaldursGate3, new GameMetaData
            {
                Game = Game.BaldursGate3,
                NexusName = "baldursgate3",
                NexusGameId = 3474,
                MO2Name = "Baldur's Gate 3",
                MO2ArchiveName = "baldursgate3",
                SteamIDs = [1086940],
                GOGIDs = [1456460669],
                IsGenericMO2Plugin = true,
                RequiredFiles = new []
                {
                    @"bin/bg3.exe".ToRelativePath()
                },
                MainExecutable = @"bin/bg3.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/cdb3fcd3d3fde62fe3b549a90793467e.png"

            }
        },
        {
            Game.Starfield, new GameMetaData
            {
                Game = Game.Starfield,
                NexusName = "starfield",
                NexusGameId = 4187,
                MO2Name = "Starfield",
                MO2ArchiveName = "Starfield",
                SteamIDs = [1716740],
                RequiredFiles = new []
                {
                    @"Starfield.exe".ToRelativePath()
                },
                MainExecutable = @"Starfield.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/1a495bc86abe171f690e27192ea6c367.png"
            }
        },
        {
            Game.SevenDaysToDie, new GameMetaData
            {
                Game = Game.SevenDaysToDie,
                MO2Name = "7 Days to Die",
                NexusName = "7daystodie",
                NexusGameId = 1059,
                MO2ArchiveName = "7daystodie",
                SteamIDs = [251570],
                RequiredFiles = new []
                {
                    @"7DaysToDie.exe".ToRelativePath(),
                    @"7dLauncher.exe".ToRelativePath(),
                },
                MainExecutable = @"7DaysToDie.exe".ToRelativePath(),
                IconSource = "https://cdn2.steamgriddb.com/icon/2b1f462a660e29c47acdcc25cb14d321.png", 
            }
        },
        {
            Game.ModdingTools, new GameMetaData
            {
                Game = Game.ModdingTools,
                MO2Name = "Modding Tools",
                MO2ArchiveName = "site",
                NexusName = "site",
                NexusGameId = 2295,
                IsGenericMO2Plugin = false,
            }
        }
       
    };

    public static ILookup<string, GameMetaData> ByNexusName = Games.Values.ToLookup(g => g.NexusName ?? "");

    public static GameMetaData? GetByMO2ArchiveName(string gameName)
    {
        return Games.Values.FirstOrDefault(g => (g.MO2ArchiveName ?? g.NexusName ?? "")!.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
    }

    public static GameMetaData? GetByNexusName(string gameName)
    {
        return Games.Values.FirstOrDefault(g => g.NexusName == gameName.ToLower());
    }

    public static GameMetaData? GetBySteamID(int id)
    {
        return Games.Values
            .FirstOrDefault(g => g.SteamIDs.Length > 0 && g.SteamIDs.Any(i => i == id));
    }

    /// <summary>
    ///     Parse game data from an arbitrary string. Tries first via parsing as a game Enum, then by Nexus name.
    ///     <param nambe="someName">Name to query</param>
    ///     <returns>GameMetaData found</returns>
    ///     <exception cref="ArgumentNullException">If string could not be translated to a game</exception>
    /// </summary>
    public static GameMetaData GetByFuzzyName(string someName)
    {
        return TryGetByFuzzyName(someName) ??
               throw new ArgumentNullException(nameof(someName), $"\"{someName}\" could not be translated to a game!");
    }

    private static GameMetaData? GetByMO2Name(string gameName)
    {
        gameName = gameName.ToLower();
        return Games.Values.FirstOrDefault(g => g.MO2Name?.ToLower() == gameName);
    }

    /// <summary>
    ///     Tries to parse game data from an arbitrary string. Tries first via parsing as a game Enum, then by Nexus name.
    ///     <param nambe="someName">Name to query</param>
    ///     <returns>GameMetaData if found, otherwise null</returns>
    /// </summary>
    public static GameMetaData? TryGetByFuzzyName(string someName)
    {
        if (Enum.TryParse(typeof(Game), someName, true, out var metadata)) return ((Game) metadata!).MetaData();

        var result = GetByNexusName(someName);
        if (result != null) return result;

        result = GetByMO2ArchiveName(someName);
        if (result != null) return result;

        result = GetByMO2Name(someName);
        if (result != null) return result;


        return int.TryParse(someName, out var id) ? GetBySteamID(id) : null;
    }

    public static bool TryGetByFuzzyName(string someName, out GameMetaData gameMetaData)
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

    public static GameMetaData MetaData(this Game game)
    {
        return Games[game];
    }
}
