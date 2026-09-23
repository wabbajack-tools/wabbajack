using System.Text;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation;

public sealed class ProfileText
{
    private ProfileText(List<string> lines, string newLine, bool trailingNewLine)
    {
        Lines = lines;
        NewLine = newLine;
        TrailingNewLine = trailingNewLine;
    }

    public List<string> Lines { get; }
    public string NewLine { get; }
    public bool TrailingNewLine { get; }

    public static ProfileText Parse(string text)
    {
        var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split(newLine).ToList();
        var trailing = lines.Count > 0 && lines[^1].Length == 0;
        if (trailing) lines.RemoveAt(lines.Count - 1);
        return new ProfileText(lines, newLine, trailing);
    }

    public override string ToString() => string.Join(NewLine, Lines) + (TrailingNewLine ? NewLine : "");

    public static async Task<ProfileText> Load(AbsolutePath path) =>
        Parse(path.FileExists() ? Encoding.UTF8.GetString(await path.ReadAllBytesAsync()) : "");

    public async Task Save(AbsolutePath path) =>
        await path.WriteAllBytesAsync(new UTF8Encoding(false).GetBytes(ToString()));

    public string? GetIniValue(string key)
    {
        foreach (var line in Lines)
        {
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return line[(eq + 1)..].Trim();
        }

        return null;
    }

    public void SetIniValue(string section, string key, string value)
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            var eq = Lines[i].IndexOf('=');
            if (eq > 0 && Lines[i][..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                Lines[i] = Lines[i][..eq] + "=" + value;
                return;
            }
        }

        var header = Lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (header < 0)
        {
            Lines.Add($"[{section}]");
            Lines.Add($"{key}={value}");
            return;
        }

        Lines.Insert(header + 1, $"{key}={value}");
    }

    public void RemoveIniValue(string key)
    {
        Lines.RemoveAll(l =>
        {
            var eq = l.IndexOf('=');
            return eq > 0 && l[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
        });
    }

    public IReadOnlyList<string> GetIniList(string key) =>
        (GetIniValue(key) ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    public void SetIniList(string section, string key, IEnumerable<string> items) =>
        SetIniValue(section, key, string.Join(", ", items));

    public void EditIniList(string section, string key, Func<List<string>, List<string>> edit) =>
        SetIniList(section, key, edit(GetIniList(key).ToList()));

    public void SetModEnabledAtTop(string modName)
    {
        RemoveModEntry(modName);
        var at = Lines.Count > 0 && Lines[0].StartsWith('#') ? 1 : 0;
        Lines.Insert(at, "+" + modName);
    }

    public void RemoveModEntry(string modName) =>
        Lines.RemoveAll(l => l.Length > 1 && (l[0] == '+' || l[0] == '-') &&
                             l[1..].Equals(modName, StringComparison.OrdinalIgnoreCase));

    public void SetPluginLast(string plugin, bool activeMarker)
    {
        RemovePlugin(plugin);
        Lines.Add(activeMarker ? "*" + plugin : plugin);
    }

    public void RemovePlugin(string plugin) =>
        Lines.RemoveAll(l => l.TrimStart('*').Trim().Equals(plugin, StringComparison.OrdinalIgnoreCase));
}
