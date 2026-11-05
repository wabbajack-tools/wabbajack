using System;

namespace Wabbajack.Paths;

/// <summary>
///     How paths are spelled for the Windows programs a modlist runs (Mod Organizer 2, the game, its tools). On
///     Windows that is just the path. On Linux those programs run under Wine or Proton, which maps the Unix root to
///     drive Z:, so <c>/home/me/Lists/Skyrim</c> is <c>Z:\home\me\Lists\Skyrim</c> to them. Anything written into a
///     config file one of them reads has to use that spelling, and anything read back out of one has to undo it.
/// </summary>
public static class WindowsProgramPaths
{
    /// <summary>The drive Wine maps the Unix root to by default (<c>dosdevices/z:</c> links to <c>/</c>).</summary>
    public const char WineRootDrive = 'Z';

    /// <summary>This path as a Windows program on this machine names it.</summary>
    public static string ToWindowsProgramPath(this AbsolutePath path)
    {
        if (path.PathFormat != PathFormat.Unix || path.Depth == 0) return path.ToString();
        return WineRootDrive + @":\" + string.Join('\\', path.PathParts);
    }

    /// <summary>A path a Windows program wrote, as this machine names it.</summary>
    public static AbsolutePath FromWindowsProgramPath(string? path)
    {
        return FromWindowsProgramPath(path, !OperatingSystem.IsWindows());
    }

    /// <summary>
    ///     A path a Windows program wrote, as a machine names it. On a Unix host a path on the Wine root drive
    ///     becomes a Unix path; any other path is taken as it is.
    /// </summary>
    public static AbsolutePath FromWindowsProgramPath(string? path, bool unixHost)
    {
        if (string.IsNullOrWhiteSpace(path)) return default;
        if (unixHost && IsOnWineRootDrive(path))
            return ("/" + path[3..].Replace('\\', '/')).ToAbsolutePath();
        return path.ToAbsolutePath();
    }

    private static bool IsOnWineRootDrive(string? path)
    {
        return path is {Length: >= 3} && char.ToUpperInvariant(path[0]) == WineRootDrive && path[1] == ':' &&
               path[2] is '\\' or '/';
    }
}
