using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace Wabbajack.Reporting;

/// <summary>
///     The three colours an article page is painted in, as CSS hex strings. Passed in rather than read
///     here so the page can wear the host's theme without this project knowing what a
///     <c>SolidColorBrush</c> is.
/// </summary>
public readonly record struct ArticleTheme(string Foreground, string Background, string Accent)
{
    public static ArticleTheme Default => new("#FFFFFF", "#1E1E1E", "#5B9BD5");
}

/// <summary>
///     Renders a <see cref="DiagnosticResult" /> - a community-authored troubleshooting article matched
///     against the user's log - into a page the system browser can open, and into the one line of it the
///     failure screen shows inline.
///     <para>
///         These articles carry headings, bold, links to downloads and, for a handful of them, an image,
///         so the app used to draw them with an embedded WebView2. With WebView2 gone the markup still has
///         to survive, so the same HTML is written to a temp file and handed to the browser instead. The
///         pipeline and stylesheet below are the ones that control used, moved here so they can be tested
///         without a window.
///     </para>
/// </summary>
public static class DiagnosticsArticle
{
    /// <summary>How much of the article the failure screen shows before sending the user to the browser.</summary>
    public const int DefaultLeadLength = 320;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    /// <summary>Markdown link syntax, reduced to its text when the lead is flattened.</summary>
    private static readonly Regex MarkdownLink = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);

    /// <summary>Emphasis and code markers, dropped from the lead.</summary>
    private static readonly Regex InlineMarkup = new(@"(\*\*|__|\*|_|`)", RegexOptions.Compiled);

    /// <summary>
    ///     The whole article as a standalone page.
    ///     <para>
    ///         <paramref name="imageUrl" /> is the screenshot a tag may carry alongside its text. It reaches
    ///         the page here because there is nowhere else for it to go: it is a field of
    ///         <see cref="DiagnosticResult" /> rather than part of the markdown body, and the embedded
    ///         control never showed it.
    ///     </para>
    /// </summary>
    public static string BuildHtml(string title, string markdown, string? imageUrl, ArticleTheme theme)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            markdown = "_No details provided._";

        var html = Markdown.ToHtml(markdown, Pipeline);

        if (TryImageTag(imageUrl, out var imageTag))
            html += imageTag;

        var pageTitle = string.IsNullOrWhiteSpace(title) ? "Wabbajack diagnostics" : title;

        var css = $@"
:root {{ color-scheme: dark; }}
body {{
  margin: 0; padding: 12px 16px;
  background: {theme.Background};
  color: {theme.Foreground};
  font-family: Segoe UI, system-ui, -apple-system, Arial, sans-serif;
  font-size: 14px; line-height: 1.6;
}}
a {{ color: {theme.Accent}; text-decoration: none; }}
a:hover {{ text-decoration: underline; }}
img {{ max-width: 100%; height: auto; border-radius: 6px; }}
pre, code {{ font-family: Consolas, 'Cascadia Code', monospace; }}
pre {{ padding: 8px 10px; overflow: auto; background: rgba(255,255,255,0.06); border-radius: 6px; }}
blockquote {{ margin: 0; padding: 8px 12px; border-left: 3px solid {theme.Accent}; background: rgba(255,255,255,0.04); border-radius: 4px; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid rgba(255,255,255,0.12); padding: 6px 8px; }}
ul, ol {{ margin: 0 0 0 20px; }}
h1, h2, h3, h4 {{ margin: 8px 0; }}
";

        return $@"<!doctype html>
<html>
  <head>
    <meta charset=""utf-8"">
    <title>{WebUtility.HtmlEncode(pageTitle)}</title>
    <style>{css}</style>
  </head>
  <body>{html}</body>
</html>";
    }

    /// <summary>
    ///     The opening paragraph, flattened to something a <c>TextBlock</c> can show, so the failure screen
    ///     says what the article is about before the user decides to open it.
    /// </summary>
    public static string Lead(string markdown, int maxLength = DefaultLeadLength)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var paragraph = new List<string>();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                // A blank line ends the first paragraph, but only once one has started - leading blanks and
                // headings above it are skipped rather than counted as the paragraph.
                if (paragraph.Count > 0) break;
                continue;
            }

            // Headings, images, quote markers, list bullets, rules and raw HTML are scaffolding rather than
            // the sentence being looked for.
            if (line.StartsWith('#') || line.StartsWith("![") || line.StartsWith('>') || line.StartsWith('<') ||
                line.StartsWith("---") || line.StartsWith("***") || line.StartsWith("|"))
            {
                if (paragraph.Count > 0) break;
                continue;
            }

            if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
                line = line[2..].Trim();

            paragraph.Add(line);
        }

        if (paragraph.Count == 0) return string.Empty;

        var text = MarkdownLink.Replace(string.Join(' ', paragraph), "$1");
        text = InlineMarkup.Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Trim();

        if (text.Length <= maxLength) return text;

        // Cut on a word boundary so the ellipsis does not land mid-word.
        var cut = text.LastIndexOf(' ', Math.Min(maxLength, text.Length - 1));
        if (cut <= 0) cut = maxLength;
        return string.Concat(text.AsSpan(0, cut).TrimEnd(), "…");
    }

    /// <summary>
    ///     A file name for the page, so the browser tab and the temp folder say what the article is rather
    ///     than carrying a random name. Anything a file name cannot hold becomes a separator, and a title
    ///     made entirely of those still leaves something usable.
    /// </summary>
    public static string FileName(string title)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = new string((title ?? string.Empty)
            .Select(c => invalid.Contains(c) || c == '.' ? ' ' : c)
            .ToArray());

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = string.Join('-', parts);

        if (name.Length > 64) name = name[..64].TrimEnd('-');
        if (name.Length == 0) name = "wabbajack-diagnostics";

        return name + ".html";
    }

    private static bool TryImageTag(string? imageUrl, out string tag)
    {
        tag = string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;

        // A tag's image is either a URL off the article repository or a path beside the executable; both
        // have to become something an href can hold before they reach the page.
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile)
            return false;

        tag = $"\n<p><img src=\"{WebUtility.HtmlEncode(uri.AbsoluteUri)}\" alt=\"\"></p>";
        return true;
    }
}
