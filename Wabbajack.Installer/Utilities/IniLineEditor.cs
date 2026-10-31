using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Wabbajack.Installer.Utilities;

/// <summary>
///     Changes values in an ini file line by line and leaves every other byte as it was.
///
///     Game prefs files are not strict ini. Fallout New Vegas writes string values that run over several
///     lines, which an ini parser rejects outright, and writing a file back through a parser reformats it
///     and drops whatever the parser did not understand. Section and key names compare ignoring case, as
///     the games read them. A key that appears more than once in a section has every copy changed.
/// </summary>
public static class IniLineEditor
{
    private static readonly Regex SectionLine = new(@"^\s*\[\s*(?<name>[^\]]*?)\s*\]");
    private static readonly Regex KeyLine = new(@"^(?<lead>\s*(?<name>[^=;#\[\s][^=]*?)\s*=[ \t]*)(?<value>[^\r\n]*)");

    public static bool Contains(string text, string section, string key)
    {
        var found = false;
        Walk(text, section, key, (line, _) =>
        {
            found = true;
            return line;
        });
        return found;
    }

    /// <summary>The text with every <paramref name="key" /> in <paramref name="section" /> set to <paramref name="value" />.</summary>
    public static string Set(string text, string section, string key, string value)
    {
        return Walk(text, section, key, (line, match) =>
            line[..match.Groups["value"].Index] + value + line[(match.Groups["value"].Index + match.Groups["value"].Length)..]);
    }

    private static string Walk(string text, string section, string key, Func<string, Match, string> onKey)
    {
        // Split after each newline so a line keeps its own terminator, CRLF or LF, and the text
        // reassembles exactly.
        var lines = Regex.Split(text, @"(?<=\n)");
        var output = new StringBuilder(text.Length);
        string? current = null;

        foreach (var line in lines)
        {
            var sectionMatch = SectionLine.Match(line);
            if (sectionMatch.Success)
            {
                current = sectionMatch.Groups["name"].Value;
                output.Append(line);
                continue;
            }

            var keyMatch = KeyLine.Match(line);
            if (keyMatch.Success
                && string.Equals(current, section, StringComparison.OrdinalIgnoreCase)
                && string.Equals(keyMatch.Groups["name"].Value, key, StringComparison.OrdinalIgnoreCase))
            {
                output.Append(onKey(line, keyMatch));
                continue;
            }

            output.Append(line);
        }

        return output.ToString();
    }
}
