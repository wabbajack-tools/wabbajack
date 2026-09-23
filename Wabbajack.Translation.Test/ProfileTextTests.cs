using Xunit;

namespace Wabbajack.Translation.Test;

public class ProfileTextTests
{
    [Fact]
    public void KeepsLineEndingsAndTrailingNewLine()
    {
        var text = ProfileText.Parse("[General]\r\nsLanguage=en\r\n");
        text.SetIniValue("General", "sLanguage", "de");
        Assert.Equal("[General]\r\nsLanguage=de\r\n", text.ToString());
    }

    [Fact]
    public void AddsAMissingKeyUnderItsSection()
    {
        var text = ProfileText.Parse("[Display]\nfoo=1\n[General]\nbar=2\n");
        text.SetIniValue("General", "sLanguage", "fr");
        Assert.Equal("[Display]\nfoo=1\n[General]\nsLanguage=fr\nbar=2\n", text.ToString());
    }

    [Fact]
    public void MatchesKeysCaseInsensitively()
    {
        var text = ProfileText.Parse("SResourceArchiveList2=Fallout4 - Animations.ba2");
        Assert.Equal(["Fallout4 - Animations.ba2"], text.GetIniList("sResourceArchiveList2"));
        text.EditIniList("Archive", "sResourceArchiveList2", l => [.. l, "DLCCoast - Voices_en.ba2"]);
        Assert.Equal("SResourceArchiveList2=Fallout4 - Animations.ba2, DLCCoast - Voices_en.ba2", text.ToString());
    }

    [Fact]
    public void PutsAModAtTopBelowTheHeaderAndReplacesAnEarlierEntry()
    {
        var text = ProfileText.Parse("# header\r\n+Other\r\n-Wabbajack Translation (German)\r\n");
        text.SetModEnabledAtTop("Wabbajack Translation (German)");
        Assert.Equal("# header\r\n+Wabbajack Translation (German)\r\n+Other\r\n", text.ToString());
    }

    [Fact]
    public void MovesAPluginToTheEnd()
    {
        var text = ProfileText.Parse("# header\n*Patch.esp\n*Other.esp\n");
        text.SetPluginLast("Patch.esp", true);
        Assert.Equal("# header\n*Other.esp\n*Patch.esp\n", text.ToString());
    }
}

public class TranslationRulesTests
{
    [Theory]
    [InlineData("Iron Sword", "Iron Sword", "Eisenschwert", FieldDecision.Apply)]
    [InlineData("Renamed Sword", "Iron Sword", "Eisenschwert", FieldDecision.OverriddenLater)]
    [InlineData("Iron Sword", "Iron Sword", null, FieldDecision.NoTranslation)]
    [InlineData("Iron Sword", "Iron Sword", " ", FieldDecision.NoTranslation)]
    [InlineData("Iron Sword", "Iron Sword", "Iron Sword", FieldDecision.Untranslated)]
    public void Decide(string winning, string owner, string? translated, FieldDecision expected) =>
        Assert.Equal(expected, TranslationRules.Decide(winning, owner, translated));

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(4, 1, false)]
    [InlineData(4, 2, false)]
    [InlineData(4, 3, true)]
    [InlineData(573, 26, false)]
    [InlineData(12181, 11471, true)]
    public void IsTranslationOf(int matched, int differing, bool expected) =>
        Assert.Equal(expected, TranslationRules.IsTranslationOf(matched, differing));

    [Theory]
    [InlineData("$MCM_Title", false)]
    [InlineData("", false)]
    [InlineData("Iron Sword", true)]
    public void IsTranslatable(string text, bool expected) =>
        Assert.Equal(expected, TranslationRules.IsTranslatable(text));
}
