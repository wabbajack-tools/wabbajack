using Mutagen.Bethesda.Strings;
using Wabbajack.DTOs;

namespace Wabbajack.Translation;

public sealed record IniSetting(string Section, string Key, string Value);

public sealed record LanguagePack(long ModId, string? FileNameContains, string Name);

public sealed record TranslationLanguage(string Id, string DisplayName, IReadOnlyList<string> NexusLanguageNames)
{
    public override string ToString() => DisplayName;
}

public abstract record VoiceSource(string Description);

public sealed record SteamVoices(string Code, string Description) : VoiceSource(Description);

public sealed record NexusVoices(IReadOnlyList<LanguagePack> Packs, string Description) : VoiceSource(Description);

public sealed record GameLanguage(
    TranslationLanguage Language,
    string SLanguage,
    string StringsSuffix,
    Language MutagenLanguage,
    VoiceSource? Voices,
    IReadOnlyList<LanguagePack> BasePacks,
    IReadOnlyList<IniSetting> IniSettings,
    IReadOnlyList<int> LegacyCodePages)
{
    public string Id => Language.Id;
    public string DisplayName => Language.DisplayName;
    public IReadOnlyList<string> NexusLanguageNames => Language.NexusLanguageNames;
    public bool HasVoices => Voices != null;
    public override string ToString() => DisplayName;
}

public static class TranslationLanguages
{
    public static readonly TranslationLanguage French = new("french", "French", ["French"]);
    public static readonly TranslationLanguage Spanish = new("spanish", "Spanish", ["Spanish (Spain)"]);
    public static readonly TranslationLanguage German = new("german", "German", ["German"]);
    public static readonly TranslationLanguage Italian = new("italian", "Italian", ["Italian"]);
    public static readonly TranslationLanguage Russian = new("russian", "Russian", ["Russian"]);
    public static readonly TranslationLanguage Polish = new("polish", "Polish", ["Polish"]);
    public static readonly TranslationLanguage Japanese = new("japanese", "Japanese", ["Japanese"]);

    public static readonly TranslationLanguage PortugueseBrazil =
        new("brazilian", "Portuguese (PT-BR)", ["Portuguese (Brazil)", "Portuguese (Portugal)"]);

    public static readonly TranslationLanguage SimplifiedChinese =
        new("schinese", "Simplified Chinese", ["Simplified Chinese"]);

    public static IReadOnlyList<TranslationLanguage> All { get; } =
        [French, Spanish, German, Italian, Russian, Polish, Japanese, PortugueseBrazil, SimplifiedChinese];

    public static TranslationLanguage? Find(string idOrName) =>
        All.FirstOrDefault(l => l.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                                l.DisplayName.Equals(idOrName, StringComparison.OrdinalIgnoreCase));
}
