using System;
using System.Collections.Generic;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>How the loaded library wants to be initialised.</summary>
public enum SteamApiInitKind
{
    /// <summary>Nothing recognisable is exported.</summary>
    None,

    /// <summary>
    ///     SDK 1.59 and later: <c>ESteamAPIInitResult SteamAPI_InitFlat(char* pOutErrMsg)</c>, where 0 is
    ///     success and the buffer has to be at least 1024 bytes.
    /// </summary>
    Flat,

    /// <summary>Everything before that: <c>bool SteamAPI_Init()</c>.</summary>
    Legacy
}

/// <summary>
///     Works out which Steamworks generation a given <c>steam_api64.dll</c> is, by looking rather than by
///     assuming.
///     <para>
///         This has to be probed rather than hardcoded because the library is the one the game shipped with,
///         and games ship with whatever was current when they were built. Skyrim Special Edition's copy
///         exports <c>SteamAPI_Init</c>, <c>SteamAPI_SteamUser_v021</c>, <c>SteamAPI_SteamApps_v008</c> and
///         <c>SteamAPI_SteamUtils_v010</c>, and no <c>SteamAPI_InitFlat</c> at all; a current SDK exports
///         <c>InitFlat</c>, <c>v023</c>, <c>v009</c> and <c>v011</c>. Pinning either set breaks the other, and
///         a game that updates breaks the pin whichever one was chosen.
///     </para>
///     <para>
///         Probing descends, so the newest interface the library actually has is the one that gets used. An
///         accessor that resolves but hands back null counts as absent: Steam declining an interface is not
///         the same as the symbol being missing, and either way the next version down is worth trying.
///     </para>
/// </summary>
public static class SteamApiInterfaceProbe
{
    /// <summary>Interface names as they appear between <c>SteamAPI_Steam</c> and the version suffix.</summary>
    public const string UserInterface = "User";

    public const string AppsInterface = "Apps";
    public const string UtilsInterface = "Utils";

    /// <summary>
    ///     The range probed, wide on both ends on purpose. The low end is below anything a supported game
    ///     ships, the high end is above anything Valve has published, and walking a few dozen exports that are
    ///     not there costs nothing measurable next to the IPC round trip that follows.
    /// </summary>
    public const int LowestVersion = 5;

    public const int HighestVersion = 30;

    public const string FlatInitExport = "SteamAPI_InitFlat";
    public const string LegacyInitExport = "SteamAPI_Init";

    /// <summary>The accessor names for one interface, newest first.</summary>
    public static IEnumerable<string> Candidates(string @interface)
    {
        for (var version = HighestVersion; version >= LowestVersion; version--)
            yield return ExportName(@interface, version);
    }

    public static string ExportName(string @interface, int version)
    {
        return $"SteamAPI_Steam{@interface}_v{version:D3}";
    }

    /// <summary>
    ///     Resolves one interface, returning the pointer Steam handed back and, through
    ///     <paramref name="resolvedExport" />, the accessor that produced it - which is worth logging, since it
    ///     names the SDK generation the game is on.
    /// </summary>
    public static IntPtr Resolve(ISteamApiExports exports, string @interface, out string? resolvedExport)
    {
        foreach (var name in Candidates(@interface))
        {
            var accessor = exports.GetExport(name);
            if (accessor == IntPtr.Zero) continue;

            var handle = exports.CallInterfaceAccessor(accessor);
            if (handle == IntPtr.Zero) continue;

            resolvedExport = name;
            return handle;
        }

        resolvedExport = null;
        return IntPtr.Zero;
    }

    /// <summary>
    ///     Which init function to call. The flat one is preferred where it exists because it is the one a
    ///     current SDK means you to use and it reports why it failed; the legacy one is what older games have
    ///     and all it can say is no.
    /// </summary>
    public static SteamApiInitKind SelectInit(ISteamApiExports exports)
    {
        if (exports.GetExport(FlatInitExport) != IntPtr.Zero) return SteamApiInitKind.Flat;
        if (exports.GetExport(LegacyInitExport) != IntPtr.Zero) return SteamApiInitKind.Legacy;
        return SteamApiInitKind.None;
    }
}
