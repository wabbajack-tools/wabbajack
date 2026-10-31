using Wabbajack.Installer.Utilities;
using Xunit;

namespace Wabbajack.Installer.Test;

public class IniLineEditorTests
{
    [Fact]
    public void SetsAValueAndLeavesTheRestAlone()
    {
        var text = "[Display]\r\niSize W=1280\r\niSize H=720\r\n[General]\r\nsLanguage=ENGLISH\r\n";

        var result = IniLineEditor.Set(text, "Display", "iSize W", "2560");

        Assert.Equal("[Display]\r\niSize W=2560\r\niSize H=720\r\n[General]\r\nsLanguage=ENGLISH\r\n", result);
    }

    [Fact]
    public void KeepsMultiLineValuesAParserRejects()
    {
        // Fallout New Vegas writes this into falloutprefs.ini. The second and third lines have no '=',
        // which is what made the ini parser give up on the whole file.
        var text = "[Display]\niSize W=1280\n" +
                   "[Text]\nSMasterMismatchWarning=One of the files has changed.\n" +
                   "This may result in errors. Saving again will clear this message\n" +
                   "but not necessarily fix any errors.\n";

        var result = IniLineEditor.Set(text, "Display", "iSize W", "1920");

        Assert.Equal(text.Replace("iSize W=1280", "iSize W=1920"), result);
    }

    [Fact]
    public void OnlyChangesTheKeyInTheNamedSection()
    {
        var text = "[Display]\nfGamma=1.0\n[Imagespace]\nfGamma=1.0\n";

        var result = IniLineEditor.Set(text, "Imagespace", "fGamma", "2.0");

        Assert.Equal("[Display]\nfGamma=1.0\n[Imagespace]\nfGamma=2.0\n", result);
    }

    [Fact]
    public void MatchesSectionAndKeyIgnoringCaseAndKeepsSpacing()
    {
        var text = "[MEMORY]\nVideoMemorySizeMb = 4096\n";

        Assert.True(IniLineEditor.Contains(text, "Memory", "videomemorysizemb"));
        Assert.Equal("[MEMORY]\nVideoMemorySizeMb = 8192\n", IniLineEditor.Set(text, "Memory", "videomemorysizemb", "8192"));
    }

    [Fact]
    public void ChangesEveryCopyOfADuplicatedKey()
    {
        var text = "[Render]\nResolution=1280x720\nResolution=1600x900\n";

        Assert.Equal("[Render]\nResolution=1920x1080\nResolution=1920x1080\n",
            IniLineEditor.Set(text, "Render", "Resolution", "1920x1080"));
    }

    [Theory]
    [InlineData("[Display]\n;iSize W=1280\n")]
    [InlineData("[Display]\n#iSize W=1280\n")]
    [InlineData("iSize W=1280\n")]
    [InlineData("[General]\niSize W=1280\n")]
    [InlineData("")]
    public void DoesNotFindTheKey(string text)
    {
        Assert.False(IniLineEditor.Contains(text, "Display", "iSize W"));
        Assert.Equal(text, IniLineEditor.Set(text, "Display", "iSize W", "1920"));
    }

    [Fact]
    public void HandlesALastLineWithoutANewline()
    {
        Assert.Equal("[Display]\r\niSize H=1080", IniLineEditor.Set("[Display]\r\niSize H=720", "Display", "iSize H", "1080"));
    }
}
