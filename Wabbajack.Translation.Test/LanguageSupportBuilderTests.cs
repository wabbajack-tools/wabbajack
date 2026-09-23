using Xunit;

namespace Wabbajack.Translation.Test;

public class LanguageSupportBuilderTests
{
    [Theory]
    [InlineData(@"Interface\Translations\MCM_en.txt", "en", "de", @"Interface\Translations\MCM_de.txt")]
    [InlineData(@"interface\translations\portable junk recycler mk 2_en.txt", "en", "fr", @"interface\translations\portable junk recycler mk 2_fr.txt")]
    [InlineData(@"Strings\weapons of fate_en.dlstrings", "en", "es", @"Strings\weapons of fate_es.dlstrings")]
    [InlineData(@"Strings/Simple Everyone's Best Friend_en.STRINGS", "en", "ptbr", @"Strings/Simple Everyone's Best Friend_ptbr.STRINGS")]
    [InlineData(@"Interface\Translations\MCM_de.txt", "en", "de", null)]
    [InlineData(@"Interface\MCM_en.txt", "en", "de", null)]
    [InlineData(@"Textures\Strings\thing_en.dds", "en", "de", null)]
    [InlineData(@"Interface\Translations\SkyUI_ENGLISH.txt", "english", "german", @"Interface\Translations\SkyUI_german.txt")]
    [InlineData(@"Strings\USSEP_english.ILSTRINGS", "english", "russian", @"Strings\USSEP_russian.ILSTRINGS")]
    [InlineData(@"Interface\Translations\SkyUI_en.txt", "english", "german", null)]
    public void LocalizedName(string path, string english, string code, string? expected) =>
        Assert.Equal(expected, LanguageSupportBuilder.LocalizedName(path, english, code));
}
