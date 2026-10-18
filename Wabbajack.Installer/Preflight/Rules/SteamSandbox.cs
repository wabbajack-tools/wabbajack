using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.Installer.Preflight.Rules;

public enum SteamSandboxKind
{
    /// <summary>Steam from the distribution or Valve's installer, which sees the whole file system.</summary>
    None,

    /// <summary>Flathub's com.valvesoftware.Steam: sees what its Flatpak permissions grant.</summary>
    Flatpak,

    /// <summary>The steam snap: sees the home folder's visible contents, and removable media if connected.</summary>
    Snap
}

/// <summary>
///     Whether Proton, started by the Steam the game came from, can reach a folder. On Linux MO2 runs under
///     Proton, and a sandboxed Steam runs Proton inside its sandbox, where a folder the sandbox was not given
///     does not exist: MO2 is told its game and mods are somewhere it cannot see. The installer, running
///     outside the sandbox, never notices.
/// </summary>
public static class SteamSandbox
{
    public const string FlatpakAppId = "com.valvesoftware.Steam";

    /// <summary>
    ///     Which Steam a game came from. A game in a sandboxed Steam's own folder says so; one in a library
    ///     elsewhere is put down to the only Steam installed, if only one is.
    /// </summary>
    public static SteamSandboxKind KindOf(string gameFolder, string home, bool nativeInstalled,
        bool flatpakInstalled, bool snapInstalled)
    {
        if (IsUnder(gameFolder, FlatpakData(home))) return SteamSandboxKind.Flatpak;
        if (IsUnder(gameFolder, SnapData(home))) return SteamSandboxKind.Snap;
        if (nativeInstalled) return SteamSandboxKind.None;
        if (flatpakInstalled && !snapInstalled) return SteamSandboxKind.Flatpak;
        if (snapInstalled && !flatpakInstalled) return SteamSandboxKind.Snap;
        return SteamSandboxKind.None;
    }

    public static string FlatpakData(string home) => $"{home}/.var/app/{FlatpakAppId}";
    public static string SnapData(string home) => $"{home}/snap/steam";

    /// <summary>The filesystems= entries of what <c>flatpak info --show-permissions</c> printed.</summary>
    public static IReadOnlyList<string> FlatpakFilesystems(string permissions)
    {
        var line = permissions.Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("filesystems=", StringComparison.Ordinal));
        return line == null
            ? Array.Empty<string>()
            : line["filesystems=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    ///     Whether Flatpak Steam may write to <paramref name="path" />: MO2 writes into its own folder and its
    ///     downloads, so a read-only grant is not enough.
    /// </summary>
    /// <param name="xdgDir">The folder an xdg-download style name stands for, from user-dirs.dirs; null when unset.</param>
    public static bool FlatpakCovers(IEnumerable<string> filesystems, string path, string home,
        Func<string, string?> xdgDir)
    {
        // The app's own data folder is always there, which is where a Flatpak Steam's default library lives.
        if (IsUnder(path, FlatpakData(home))) return true;

        foreach (var entry in filesystems)
        {
            if (entry.StartsWith('!')) continue;
            var colon = entry.LastIndexOf(':');
            var location = colon < 0 ? entry : entry[..colon];
            var mode = colon < 0 ? "rw" : entry[(colon + 1)..];
            if (mode == "ro") continue;

            if (GrantedFolder(location, home, xdgDir) is { } granted && IsUnder(path, granted)) return true;
        }

        return false;
    }

    /// <summary>
    ///     The snap's home interface reaches the visible contents of the home folder, so not a folder whose name
    ///     starts with a dot, and nothing outside it.
    /// </summary>
    public static bool SnapCovers(string path, string home)
    {
        if (!IsUnder(path, home) || path.TrimEnd('/') == home.TrimEnd('/')) return false;
        var first = path[(home.TrimEnd('/').Length + 1)..].Split('/')[0];
        return !first.StartsWith('.');
    }

    /// <summary>The folder a filesystems= location grants, or null for one that grants nothing under /home.</summary>
    private static string? GrantedFolder(string location, string home, Func<string, string?> xdgDir)
    {
        if (location == "host") return "/";
        if (location == "home") return home;
        if (location.StartsWith('/')) return location;
        if (location == "~" || location.StartsWith("~/", StringComparison.Ordinal)) return home + location[1..];

        if (!location.StartsWith("xdg-", StringComparison.Ordinal)) return null; // host-os, host-etc, ...
        var slash = location.IndexOf('/');
        var name = slash < 0 ? location : location[..slash];
        var below = slash < 0 ? "" : location[slash..];
        var folder = name switch
        {
            "xdg-config" => $"{home}/.config",
            "xdg-data" => $"{home}/.local/share",
            "xdg-cache" => $"{home}/.cache",
            "xdg-run" => null,
            _ => xdgDir(name["xdg-".Length..].Replace("-", "").ToUpperInvariant())
        };
        return folder == null ? null : folder + below;
    }

    private static bool IsUnder(string path, string folder)
    {
        var trimmed = folder.TrimEnd('/');
        return trimmed.Length == 0 || path == trimmed || path.StartsWith(trimmed + "/", StringComparison.Ordinal);
    }
}
