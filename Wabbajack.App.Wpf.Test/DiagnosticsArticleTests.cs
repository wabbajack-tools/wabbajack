using System.IO;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Reporting;
using Xunit;

namespace Wabbajack.App.Wpf.Test;

/// <summary>
///     The failure screen's "How do I fix this?" article.
///     <para>
///         It used to be drawn by an embedded WebView2 through <c>NavigateToString</c>. The same HTML is now
///         written to a temp file and opened in the user's browser, so what this pins is the part that has to
///         survive that move: the markup a community-authored article carries - headings, links to downloads,
///         the screenshot a handful of them have - reaching the page rather than being flattened to text, and
///         the file and the inline summary that go with it.
///     </para>
/// </summary>
public class DiagnosticsArticleTests
{
    private static readonly ArticleTheme Theme = new("#111111", "#222222", "#333333");

    [Fact]
    public void RendersMarkdownRatherThanShowingItsSyntax()
    {
        var html = DiagnosticsArticle.BuildHtml("Possible issue: Hash Fails",
            "# Hash Fails\n\nTry **deleting** the file and downloading it again.", null, Theme);

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>deleting</strong>", html);
        Assert.DoesNotContain("**deleting**", html);
    }

    /// <summary>
    ///     Articles point at the mod or tool the user has to fetch, which is most of what makes them worth
    ///     opening at all.
    /// </summary>
    [Fact]
    public void KeepsLinks()
    {
        var html = DiagnosticsArticle.BuildHtml("Possible issue: api",
            "See [the wiki](https://wiki.wabbajack.org/index.html?tab=files&id=9) for the fix.", null, Theme);

        Assert.Contains("href=\"https://wiki.wabbajack.org/index.html?tab=files&amp;id=9\"", html);
    }

    /// <summary>
    ///     A tag's screenshot is a field of <see cref="DiagnosticResult" /> rather than part of its markdown,
    ///     so it only reaches the page if the renderer puts it there. The embedded control never did.
    /// </summary>
    [Theory]
    [InlineData("https://raw.githubusercontent.com/JanuarySnow/WJ-Bot/main/images/api.png")]
    [InlineData("http://example.com/api.png")]
    public void ShowsTheScreenshotAnArticleCarries(string url)
    {
        var html = DiagnosticsArticle.BuildHtml("Possible issue: api", "Body.", url, Theme);

        Assert.Contains($"<img src=\"{url}\"", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // ShellExecute is not reached from a page, but a javascript: or data: src is still not a screenshot.
    [InlineData("javascript:alert(1)")]
    [InlineData("images/api.png")]
    public void ShowsNoImageForAnythingThatIsNotOne(string url)
    {
        var html = DiagnosticsArticle.BuildHtml("Possible issue: api", "Body.", url, Theme);

        Assert.DoesNotContain("<img", html);
    }

    /// <summary>The theme travels with the page, so the article is not black-on-white inside a dark app.</summary>
    [Fact]
    public void PaintsThePageInTheThemeItIsGiven()
    {
        var html = DiagnosticsArticle.BuildHtml("t", "Body.", null, Theme);

        Assert.Contains("background: #222222", html);
        Assert.Contains("color: #111111", html);
        Assert.Contains("color: #333333", html);
    }

    /// <summary>
    ///     The page is a browser tab now, so it needs a name; without one the tab reads as the temp file's.
    /// </summary>
    [Fact]
    public void TitlesThePage()
    {
        var html = DiagnosticsArticle.BuildHtml("Possible issue: Hash & Fails", "Body.", null, Theme);

        Assert.Contains("<title>Possible issue: Hash &amp; Fails</title>", html);
    }

    [Fact]
    public void SaysSoWhenThereIsNoBody()
    {
        var html = DiagnosticsArticle.BuildHtml("t", "   ", null, Theme);

        Assert.Contains("No details provided", html);
    }

    [Theory]
    // The heading above the first paragraph is not the sentence the failure screen wants.
    [InlineData("# Hash Fails\n\nDelete the file and download it again.\n\nMore below.",
        "Delete the file and download it again.")]
    // Emphasis and links are flattened rather than shown as syntax.
    [InlineData("Try **deleting** the [file](https://example.com) again.",
        "Try deleting the file again.")]
    // A paragraph wrapped over several lines is one sentence.
    [InlineData("Delete the file\nand download it again.\n\nMore below.",
        "Delete the file and download it again.")]
    // A list is as good a lead as a paragraph when the article opens with one.
    [InlineData("- Delete the file\n- Download it again", "Delete the file Download it again")]
    public void LeadIsTheOpeningOfTheArticle(string markdown, string expected)
    {
        Assert.Equal(expected, DiagnosticsArticle.Lead(markdown));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# Only a heading")]
    [InlineData("![just an image](x.png)")]
    public void LeadIsEmptyWhenThereIsNoProse(string markdown)
    {
        Assert.Equal(string.Empty, DiagnosticsArticle.Lead(markdown));
    }

    [Fact]
    public void LeadStopsOnAWordBoundary()
    {
        var lead = DiagnosticsArticle.Lead("aaaa bbbb cccc dddd eeee", 12);

        Assert.Equal("aaaa bbbb…", lead);
    }

    /// <summary>
    ///     The name is what the browser tab and the temp folder show, so it has to be both readable and
    ///     writable - no separators, no drive colon, and no earlier dot for the extension to be lost behind.
    /// </summary>
    [Theory]
    [InlineData("Possible issue: Hash Fails", "Possible-issue-Hash-Fails.html")]
    [InlineData("Possible issue: path/not\\found", "Possible-issue-path-not-found.html")]
    [InlineData("Possible issue: MO2 v2.4.4 crash", "Possible-issue-MO2-v2-4-4-crash.html")]
    [InlineData("  spaced   out  ", "spaced-out.html")]
    [InlineData("", "wabbajack-diagnostics.html")]
    [InlineData("///", "wabbajack-diagnostics.html")]
    public void FileNameSaysWhatTheArticleIs(string title, string expected)
    {
        Assert.Equal(expected, DiagnosticsArticle.FileName(title));
    }

    [Fact]
    public void FileNameStaysShortEnoughToBeAPath()
    {
        var name = DiagnosticsArticle.FileName(new string('a', 300));

        Assert.Equal(64 + ".html".Length, name.Length);
    }

    /// <summary>
    ///     What <c>InstallationVM.OpenFailureArticle</c> does before it hands the path to the shell, minus
    ///     the launch: a folder from <see cref="TemporaryFileManager" />, the article's own name inside it,
    ///     and the page written to that. Pinned here because the shell will not open a page that is not a
    ///     file, and none of that is reachable from the WPF side.
    /// </summary>
    [Fact]
    public void WritesTheArticleWhereTheShellCanOpenIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "wj-article-test-" + RandomName.Next()).ToAbsolutePath();
        using var manager = new TemporaryFileManager(root);

        var html = DiagnosticsArticle.BuildHtml("Possible issue: Hash Fails", "# Hash Fails\n\nBody.", null, Theme);
        var file = manager.CreateFolder().Path.Combine(DiagnosticsArticle.FileName("Possible issue: Hash Fails"));
        file.WriteAllText(html);

        Assert.True(file.FileExists());
        Assert.Equal(Ext.Html, file.Extension);
        Assert.Equal("Possible-issue-Hash-Fails.html", file.FileName.ToString());
        Assert.Equal(html, file.ReadAllText());
    }
}
