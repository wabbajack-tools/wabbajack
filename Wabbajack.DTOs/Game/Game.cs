using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Game
{
    Morrowind,
    Oblivion,
    [Description("Fallout 3")] Fallout3,
    [Description("Fallout New Vegas")] FalloutNewVegas,

    [Description("Skyrim Legendary Edition")]
    Skyrim,
    Enderal,

    [Description("Skyrim Special Edition")]
    SkyrimSpecialEdition,
    [Description("Fallout 4")] Fallout4,
    [Description("Skyrim VR")] SkyrimVR,
    [Description("Fallout 4 VR")] Fallout4VR,
    [Description("Darkest Dungeon")] DarkestDungeon,
    Dishonored,
    [Description("Witcher: Enhanced Edition ")]Witcher,
    [Description("Witcher 3")] Witcher3,
    [Description("Stardew Valley")] StardewValley,

    [Description("Kingdom Come: Deliverance")]
    KingdomComeDeliverance,

    [Description("MechWarrior 5: Mercenaries")]
    MechWarrior5Mercenaries,
    [Description("No Man's Sky")] NoMansSky,
    [Description("Dragon Age: Origins")] DragonAgeOrigins,
    [Description("Dragon Age 2")] DragonAge2,

    [Description("Dragon Age: Inquisition")]
    DragonAgeInquisition,
    [Description("Kerbal Space Program")] KerbalSpaceProgram,

    [Description("Enderal Special Edition")]
    EnderalSpecialEdition,

    [Description("Terraria")] Terraria,
    [Description("Cyberpunk 2077")] Cyberpunk2077,
    [Description("The Sims 4")] Sims4,
    [Description("Dragons Dogma Dark Arisen")] DragonsDogma,

    [Description("Karryn's Prison")]
    KarrynsPrison,
    [Description("Mount & Blade II: Bannerlord")] MountAndBlade2Bannerlord,
    [Description("Valheim")]Valheim,
    [Description("Modding Tools")] ModdingTools,

    [Description("Final Fantasy VII Remake")] FinalFantasy7Remake,
    [Description("Baldur's Gate 3")] BaldursGate3,
    [Description("Starfield")] Starfield,
    [Description("7 Days to Die")] SevenDaysToDie,
}
