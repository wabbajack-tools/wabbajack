using Xunit;

namespace Wabbajack.App.Wpf.Test;

/// <summary>
///     Regression test for the manual download links.
///     <para>
///         <c>UIUtils.OpenWebsite</c> used to launch <c>cmd.exe /c start &lt;url&gt;</c>. <c>cmd</c> reads an
///         unquoted <c>&amp;</c> as a command separator, so it only ever saw the URL up to the first query
///         parameter and ran the rest as a second command, which failed silently behind
///         <c>CreateNoWindow</c>. Preflight's "Open page" button for a Nexus manual download therefore
///         opened <c>.../mods/266?tab=files</c> - the mod's Files tab with no file selected - rather than
///         the file link <c>ManualDownloadUrls</c> had built. Google Drive lost <c>&amp;export=download</c>
///         the same way.
///     </para>
///     <para>
///         What the shell is handed is now the URL in one piece, so this pins that and the scheme filter
///         that keeps ShellExecute from being pointed at anything but a website.
///     </para>
/// </summary>
public class OpenWebsiteTests
{
    [Theory]
    // The shape the owner asked for: the query string is the only part that names the file.
    [InlineData("https://www.nexusmods.com/skyrimspecialedition/mods/266?tab=files&file_id=209150")]
    [InlineData("https://drive.google.com/uc?id=1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_&export=download")]
    [InlineData("https://www.loverslab.com/applications/core/interface/file/attachment.php?id=853295")]
    [InlineData("https://mega.nz/file/CsMSFaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k")]
    public void HandsTheShellTheWholeUrl(string url)
    {
        Assert.Equal(url, UIUtils.WebsiteTarget(url));
    }

    /// <summary>A space in a URL is escaped rather than left to be read as an argument separator.</summary>
    [Fact]
    public void EscapesRatherThanSplitting()
    {
        Assert.Equal("https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip",
            UIUtils.WebsiteTarget("https://authored-files.wabbajack.org/Tonal Architect_WJ_TEST_FILES.zip"));
    }

    [Theory]
    [InlineData("C:\\Windows\\System32\\calc.exe")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:windowsupdate")]
    [InlineData("not a url at all")]
    [InlineData("")]
    public void OpensNothingThatIsNotAWebsite(string url)
    {
        Assert.Null(UIUtils.WebsiteTarget(url));
    }
}
