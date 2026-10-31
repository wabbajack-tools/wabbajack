using System;
using System.IO;

namespace Wabbajack.Paths.IO;

/// <summary>
///     The user folders a Linux desktop names in ~/.config/user-dirs.dirs, such as Downloads, which a user can
///     move or which the desktop names in their own language (~/Téléchargements).
/// </summary>
public static class XdgUserDirs
{
    /// <summary>
    ///     The folder for <paramref name="name" /> (DOWNLOAD, DOCUMENTS, DESKTOP, ...): $XDG_&lt;NAME&gt;_DIR when set,
    ///     else user-dirs.dirs. Null when neither names it.
    /// </summary>
    public static string? Get(string name)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Environment.GetEnvironmentVariable($"XDG_{name}_DIR") is {Length: > 0} fromEnvironment)
            return fromEnvironment;

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is {Length: > 0} config
            ? config
            : Path.Combine(home, ".config");
        try
        {
            var file = Path.Combine(configHome, "user-dirs.dirs");
            return File.Exists(file) ? Parse(File.ReadAllText(file), name, home) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    ///     XDG_&lt;NAME&gt;_DIR out of a user-dirs.dirs file. Its values are quoted, and are either absolute or start
    ///     with $HOME, which is all the format allows.
    /// </summary>
    public static string? Parse(string contents, string name, string home)
    {
        var key = $"XDG_{name}_DIR=";
        foreach (var raw in contents.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(key, StringComparison.Ordinal)) continue;

            var value = line[key.Length..].Trim().Trim('"');
            if (value.StartsWith("$HOME", StringComparison.Ordinal))
                value = home + value["$HOME".Length..];
            return value.StartsWith('/') ? value.TrimEnd('/') : null;
        }

        return null;
    }
}
