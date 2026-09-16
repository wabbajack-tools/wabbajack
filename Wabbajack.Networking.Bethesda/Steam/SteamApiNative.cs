using System;
using System.Runtime.InteropServices;
using Wabbajack.Paths;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     The handful of <c>steam_api64.dll</c> entry points minting a ticket needs, bound at runtime rather
///     than with <c>DllImport</c>.
///     <para>
///         Runtime binding is what makes version probing possible at all: the interface accessors carry their
///         version in the symbol name, so which symbol to call is not known until the library is open. It also
///         means a library missing one of these is a message rather than an <c>EntryPointNotFoundException</c>
///         at the moment of the call.
///     </para>
///     <para>
///         Every function that returns a C++ <c>bool</c> is declared here as returning <c>byte</c>. That is
///         the same single byte on the wire and it sidesteps having to reason about how a one-byte bool
///         marshals on each platform.
///     </para>
/// </summary>
internal static class SteamApiNative
{
    /// <summary>Tickets come back around 159 bytes; this is room to spare rather than a measured bound.</summary>
    public const int TicketBufferSize = 2048;

    /// <summary>
    ///     <c>SteamAPI_InitFlat</c> wants at least 1024 bytes for its error message and will write that many.
    /// </summary>
    public const int InitErrorBufferSize = 1024;

    /// <summary>
    ///     <c>EncryptedAppTicketResponse_t.k_iCallback</c>: <c>k_iSteamUserCallbacks</c> (100) + 54. The struct
    ///     is one <c>EResult</c>, so four bytes.
    /// </summary>
    public const int EncryptedAppTicketResponseCallback = 154;

    public const int EncryptedAppTicketResponseSize = 4;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte LegacyInit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int FlatInit(byte[] errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr GetInterface();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RunCallbacks();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Shutdown();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate ulong RequestEncryptedAppTicket(IntPtr self, IntPtr dataToInclude, int cbDataToInclude);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte GetEncryptedAppTicket(IntPtr self, IntPtr ticket, int cbMaxTicket, out uint cbTicket);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte BIsSubscribed(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte IsApiCallCompleted(IntPtr self, ulong call, out byte failed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte GetApiCallResult(IntPtr self, ulong call, IntPtr callback, int cubCallback,
        int callbackExpected, out byte failed);

    public const string RunCallbacksExport = "SteamAPI_RunCallbacks";
    public const string ShutdownExport = "SteamAPI_Shutdown";
    public const string RequestTicketExport = "SteamAPI_ISteamUser_RequestEncryptedAppTicket";
    public const string GetTicketExport = "SteamAPI_ISteamUser_GetEncryptedAppTicket";
    public const string IsSubscribedExport = "SteamAPI_ISteamApps_BIsSubscribed";
    public const string IsApiCallCompletedExport = "SteamAPI_ISteamUtils_IsAPICallCompleted";
    public const string GetApiCallResultExport = "SteamAPI_ISteamUtils_GetAPICallResult";
}

/// <summary>
///     <see cref="ISteamApiExports" /> over a real loaded library.
/// </summary>
/// <remarks>
///     The library is loaded by absolute path, which on Windows means the loader also searches the folder it
///     came from for anything it depends on. That is what lets the helper keep its own working directory -
///     and so keep its log file out of the user's game install - while still letting the game's copy of
///     <c>steam_api64.dll</c> find whatever sits beside it.
/// </remarks>
internal sealed class NativeSteamApiExports : ISteamApiExports, IDisposable
{
    private IntPtr _handle;

    private NativeSteamApiExports(IntPtr handle)
    {
        _handle = handle;
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        NativeLibrary.Free(_handle);
        _handle = IntPtr.Zero;
    }

    public IntPtr GetExport(string name)
    {
        return NativeLibrary.TryGetExport(_handle, name, out var address) ? address : IntPtr.Zero;
    }

    public IntPtr CallInterfaceAccessor(IntPtr export)
    {
        return Marshal.GetDelegateForFunctionPointer<SteamApiNative.GetInterface>(export)();
    }

    public static NativeSteamApiExports Load(AbsolutePath library)
    {
        return new NativeSteamApiExports(NativeLibrary.Load(library.ToString()));
    }

    public T? GetDelegate<T>(string name) where T : Delegate
    {
        var address = GetExport(name);
        return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(address);
    }
}
