using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Networking.Bethesda.Steam;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     A stand-in export table. Each name gets an address, and each address a handle, so both halves of
///     probing - the symbol being absent, and the accessor answering with nothing - can be set up by hand.
/// </summary>
public sealed class FakeSteamApiExports : ISteamApiExports
{
    private readonly Dictionary<IntPtr, IntPtr> _handles = new();
    private readonly Dictionary<string, IntPtr> _names = new(StringComparer.Ordinal);
    private int _next = 0x1000;

    public List<string> Called { get; } = new();

    public IntPtr GetExport(string name)
    {
        return _names.TryGetValue(name, out var address) ? address : IntPtr.Zero;
    }

    public IntPtr CallInterfaceAccessor(IntPtr export)
    {
        Called.Add(_names.First(p => p.Value == export).Key);
        return _handles.TryGetValue(export, out var handle) ? handle : IntPtr.Zero;
    }

    /// <summary>Exports a symbol whose accessor, when called, hands back a usable interface.</summary>
    public FakeSteamApiExports Exporting(params string[] names)
    {
        foreach (var name in names)
        {
            var address = new IntPtr(_next++);
            _names[name] = address;
            _handles[address] = new IntPtr(_next++);
        }

        return this;
    }

    /// <summary>Exports a symbol whose accessor answers with null, the way Steam does when it declines one.</summary>
    public FakeSteamApiExports ExportingDeclined(params string[] names)
    {
        foreach (var name in names) _names[name] = new IntPtr(_next++);
        return this;
    }

    /// <summary>What Skyrim Special Edition actually ships, read out of the shipped DLL's export table.</summary>
    public static FakeSteamApiExports SkyrimSpecialEdition()
    {
        return new FakeSteamApiExports().Exporting(
            SteamApiInterfaceProbe.LegacyInitExport,
            "SteamAPI_SteamUser_v021",
            "SteamAPI_SteamApps_v008",
            "SteamAPI_SteamUtils_v010");
    }

    /// <summary>What a current SDK ships, which is what the reference implementation was written against.</summary>
    public static FakeSteamApiExports CurrentSdk()
    {
        return new FakeSteamApiExports().Exporting(
            SteamApiInterfaceProbe.FlatInitExport,
            SteamApiInterfaceProbe.LegacyInitExport,
            "SteamAPI_SteamUser_v023",
            "SteamAPI_SteamApps_v009",
            "SteamAPI_SteamUtils_v011");
    }
}
