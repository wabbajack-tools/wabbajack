using Mutagen.Bethesda;
using Mutagen.Bethesda.Strings;
using Wabbajack.DTOs;
using static Wabbajack.Translation.TranslationLanguages;

namespace Wabbajack.Translation;

public sealed record SteamVoiceArchive(
    string Plugin,
    string ArchiveFormat,
    IReadOnlyDictionary<string, uint> Depots,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ulong>> ManifestsByGameVersion);

public sealed record GameTranslationSupport(
    Game Game,
    GameRelease Release,
    string Executable,
    string IniFile,
    string CccFile,
    IReadOnlyList<string> ImplicitPlugins,
    string EnglishStringsSuffix,
    uint SteamAppId,
    IReadOnlyList<SteamVoiceArchive> SteamVoices,
    string BaseVoicesIniKey,
    string EnglishBaseVoices,
    string? EnglishPluginVoicesPattern,
    string EnglishPluginVoicesIniKey,
    bool LocalizedPatch,
    IReadOnlyList<GameLanguage> Languages)
{
    public GameLanguage? Find(string idOrName) =>
        Languages.FirstOrDefault(l => l.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                                      l.DisplayName.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                                      l.SLanguage.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                                      l.StringsSuffix.Equals(idOrName, StringComparison.OrdinalIgnoreCase));

    public string VoicesArchive(string code) => string.Format(SteamVoices[0].ArchiveFormat, code);
}

public static class TranslationGames
{
    private const string Fallout4VoiceSize = "around 3.5 GB";
    private const string SkyrimVoiceSize = "around 2 GB";
    private const string OldGen = "1.10.163.0";

    private static IReadOnlyList<IniSetting> Font(string code) =>
        [new IniSetting("Fonts", "sFontConfigFile", $@"Interface\FontConfig_{code}.txt")];

    private static SteamVoices Steam(TranslationLanguage language, string code, string size) =>
        new(code, $"the official {language.DisplayName} voice files for the game and its DLC from Steam, {size}");

    public static readonly GameTranslationSupport Fallout4 = new(
        Game.Fallout4,
        GameRelease.Fallout4,
        "Fallout4.exe",
        "Fallout4.ini",
        "Fallout4.ccc",
        ["Fallout4.esm", "DLCRobot.esm", "DLCworkshop01.esm", "DLCCoast.esm", "DLCworkshop02.esm", "DLCworkshop03.esm",
            "DLCNukaWorld.esm", "DLCUltraHighResolution.esm"],
        "en",
        377160,
        [
            new SteamVoiceArchive("Fallout4.esm", "Fallout4 - Voices_{0}.ba2",
                new Dictionary<string, uint> {["fr"] = 377165, ["de"] = 377166, ["es"] = 377168},
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>
                {
                    [OldGen] = new Dictionary<string, ulong>
                        {["fr"] = 7549549550652702123, ["de"] = 6854162778963425477, ["es"] = 7717372852115364102}
                }),
            new SteamVoiceArchive("DLCRobot.esm", "DLCRobot - Voices_{0}.ba2",
                new Dictionary<string, uint> {["fr"] = 435872, ["de"] = 435873, ["es"] = 435875},
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>
                {
                    [OldGen] = new Dictionary<string, ulong>
                        {["fr"] = 5590419866095647350, ["de"] = 2207548206398235202, ["es"] = 2953236065717816833}
                }),
            new SteamVoiceArchive("DLCCoast.esm", "DLCCoast - Voices_{0}.ba2",
                new Dictionary<string, uint> {["fr"] = 435883, ["de"] = 435884, ["es"] = 435886},
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>()),
            new SteamVoiceArchive("DLCworkshop03.esm", "DLCworkshop03 - Voices_{0}.ba2",
                new Dictionary<string, uint> {["fr"] = 393886, ["de"] = 393887, ["es"] = 393889},
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>()),
            new SteamVoiceArchive("DLCNukaWorld.esm", "DLCNukaWorld - Voices_{0}.ba2",
                new Dictionary<string, uint> {["fr"] = 393896, ["de"] = 393897, ["es"] = 393898},
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>())
        ],
        "SResourceArchiveList",
        "Fallout4 - Voices.ba2",
        @" - voices_en\.ba2$",
        "SResourceArchiveList2",
        false,
        [
            new GameLanguage(French, "fr", "fr", Language.French, Steam(French, "fr", Fallout4VoiceSize), [], [], [1252]),
            new GameLanguage(Spanish, "es", "es", Language.Spanish, Steam(Spanish, "es", Fallout4VoiceSize), [], [], [1252]),
            new GameLanguage(PortugueseBrazil, "ptbr", "ptbr", Language.Portuguese_Brazil, null, [], [], [1252]),
            new GameLanguage(German, "de", "de", Language.German, Steam(German, "de", Fallout4VoiceSize), [], [], [1252]),
            new GameLanguage(SimplifiedChinese, "cn", "cn", Language.Chinese, null,
                [new LanguagePack(28854, "(CHS)", "Unofficial Fallout 4 Chinese Translation Fix")],
                [
                    new IniSetting("General", "sValidNameCharsFile", @"Interface\FontConfig_cn.txt"),
                    new IniSetting("Fonts", "sFontConfigFile", @"Interface\FontConfig_cn.txt")
                ],
                [936])
        ]);

    public static readonly GameTranslationSupport SkyrimSpecialEdition = new(
        Game.SkyrimSpecialEdition,
        GameRelease.SkyrimSE,
        "SkyrimSE.exe",
        "skyrim.ini",
        "Skyrim.ccc",
        ["Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm", "_ResourcePack.esl"],
        "english",
        489830,
        [
            new SteamVoiceArchive("Skyrim.esm", "Skyrim - Voices_{0}0.bsa",
                new Dictionary<string, uint>
                {
                    ["fr"] = 489834, ["it"] = 489835, ["de"] = 489836, ["es"] = 489837, ["ru"] = 489838,
                    ["pl"] = 489839, ["ja"] = 544861
                },
                new Dictionary<string, IReadOnlyDictionary<string, ulong>>())
        ],
        "sResourceArchiveList2",
        "Skyrim - Voices_en0.bsa",
        null,
        "sResourceArchiveList2",
        true,
        [
            new GameLanguage(French, "FRENCH", "french", Language.French, Steam(French, "fr", SkyrimVoiceSize), [], [], [1252]),
            new GameLanguage(Spanish, "SPANISH", "spanish", Language.Spanish, Steam(Spanish, "es", SkyrimVoiceSize), [], [], [1252]),
            new GameLanguage(German, "GERMAN", "german", Language.German, Steam(German, "de", SkyrimVoiceSize), [], [], [1252]),
            new GameLanguage(Italian, "ITALIAN", "italian", Language.Italian, Steam(Italian, "it", SkyrimVoiceSize), [], [], [1252]),
            new GameLanguage(Russian, "RUSSIAN", "russian", Language.Russian, Steam(Russian, "ru", SkyrimVoiceSize), [], Font("ru"), [1251]),
            new GameLanguage(Polish, "POLISH", "polish", Language.Polish, Steam(Polish, "pl", SkyrimVoiceSize), [], Font("pl"), [1250]),
            new GameLanguage(Japanese, "JAPANESE", "japanese", Language.Japanese, Steam(Japanese, "ja", SkyrimVoiceSize), [], Font("ja"), [932]),
            // No official PT-BR or Simplified Chinese the packs replace the English strings, so the game stays in English.
            new GameLanguage(PortugueseBrazil, "ENGLISH", "english", Language.English,
                new NexusVoices(
                    [
                        new LanguagePack(105861, null, "Dublagem Skyrim PT-BR"),
                        new LanguagePack(117781, null, "(UPDATE) Dublagem Skyrim PT-BR")
                    ],
                    "the Brazilian Portuguese fan dub \"Dublagem Skyrim PT-BR\" from Nexus Mods, around 2.6 GB"),
                [new LanguagePack(714, null, "PTBR Skyrim Special Edition Traducao")],
                [],
                [1252]),
            new GameLanguage(SimplifiedChinese, "ENGLISH", "english", Language.English, null,
                [new LanguagePack(1333, null, "Unofficial Chinese Translation for Skyrim Special Edition")],
                [],
                [936])
        ]);

    public static IReadOnlyList<GameTranslationSupport> All { get; } = [Fallout4, SkyrimSpecialEdition];

    public static GameTranslationSupport? For(Game game) => All.FirstOrDefault(g => g.Game == game);
}
