using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     Mints an encrypted app ticket in <em>this</em> process, by loading the game's own copy of
///     <c>steam_api64.dll</c> and asking the Steam client behind it.
///     <para>
///         Nothing in the app calls this directly. <c>SteamAPI_Init</c> is process-global and wants
///         <c>SteamAppId</c> in the environment, so whatever calls it registers with the running Steam client
///         <em>as the game</em> for the rest of its life - the user's friends would see them in Skyrim - and
///         takes a native library of unknown vintage into its address space, where a bad one ends the process
///         rather than the request. So this runs in a short-lived child (the <c>steam-app-ticket</c> CLI
///         verb), which prints the ticket and exits, and <see cref="ChildProcessSteamAppTicketSource" /> is
///         what everything else uses. Both costs then last about a second.
///     </para>
/// </summary>
public static class SteamAppTicketMinter
{
    /// <summary>The Steamworks redistributable every supported game ships in its own folder.</summary>
    public const string LibraryName = "steam_api64.dll";

    /// <summary>
    ///     The payload the game itself sends: four zero bytes. Steamworks allows application data to be
    ///     folded into the ticket here, and Bethesda's servers expect the shape the game produces, so this
    ///     matches it rather than improving on it.
    /// </summary>
    private static readonly byte[] TicketPayload = new byte[4];

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Asks the Steam client for a ticket for <paramref name="appId" />, using the library that ships in
    ///     <paramref name="gameFolder" />. Throws <see cref="SteamAppTicketException" /> for every way this can
    ///     fail to produce one.
    /// </summary>
    public static byte[] Mint(AbsolutePath gameFolder, uint appId, string gameName, TimeSpan timeout,
        ILogger logger, CancellationToken token)
    {
        var library = gameFolder.Combine(LibraryName);
        if (!library.FileExists())
            throw new SteamAppTicketException(SteamAppTicketError.SteamApiMissing,
                $"{library} does not exist. {SteamAppTicketError.SteamApiMissing.DefaultMessage(gameName)}");

        // Asked before anything is loaded, because "Steam is not running" is the one failure with an obvious
        // fix, and a Steamworks init against a dead client reports it as an unexplained false.
        if (!IsSteamClientRunning())
            throw new SteamAppTicketException(SteamAppTicketError.SteamNotRunning,
                SteamAppTicketError.SteamNotRunning.DefaultMessage(gameName));

        // Both of these have to be in the environment before init: they are how a process that Steam did not
        // launch says which app it is speaking for.
        var id = appId.ToString(CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("SteamAppId", id);
        Environment.SetEnvironmentVariable("SteamGameId", id);

        NativeSteamApiExports exports;
        try
        {
            exports = NativeSteamApiExports.Load(library);
        }
        catch (Exception ex)
        {
            throw new SteamAppTicketException(SteamAppTicketError.SteamApiMissing,
                $"{library} could not be loaded: {ex.Message}", ex);
        }

        var initialised = false;
        try
        {
            Initialise(exports, gameName, logger);
            initialised = true;
            return Request(exports, gameName, timeout, logger, token);
        }
        finally
        {
            // Shut down on the way out whatever happened, so the Steam client stops showing the account as
            // playing the game the moment this is over rather than when it notices the process died.
            if (initialised) SafeShutdown(exports, logger);
            exports.Dispose();
        }
    }

    /// <summary>
    ///     Whether a Steam client is running. Deliberately a process lookup rather than anything cleverer:
    ///     the question is only ever asked to turn one blank refusal into a sentence worth reading.
    /// </summary>
    public static bool IsSteamClientRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("steam");
        }
        catch (Exception)
        {
            // Enumerating processes can be refused outright on a locked-down machine. Not knowing is not the
            // same as knowing Steam is absent, so let the init attempt be the judge.
            return true;
        }

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static void Initialise(NativeSteamApiExports exports, string gameName, ILogger logger)
    {
        switch (SteamApiInterfaceProbe.SelectInit(exports))
        {
            case SteamApiInitKind.Flat:
            {
                var init = exports.GetDelegate<SteamApiNative.FlatInit>(SteamApiInterfaceProbe.FlatInitExport)!;
                var error = new byte[SteamApiNative.InitErrorBufferSize];
                var result = init(error);
                if (result != 0)
                    throw new SteamAppTicketException(SteamAppTicketError.InitFailed,
                        $"{SteamAppTicketError.InitFailed.DefaultMessage(gameName)} " +
                        $"(SteamAPI_InitFlat returned {result}: {ReadUtf8(error)})");

                logger.LogDebug("Initialised Steamworks through SteamAPI_InitFlat");
                return;
            }

            case SteamApiInitKind.Legacy:
            {
                var init = exports.GetDelegate<SteamApiNative.LegacyInit>(SteamApiInterfaceProbe.LegacyInitExport)!;
                if (init() == 0)
                    throw new SteamAppTicketException(SteamAppTicketError.InitFailed,
                        $"{SteamAppTicketError.InitFailed.DefaultMessage(gameName)} (SteamAPI_Init returned false)");

                logger.LogDebug("Initialised Steamworks through SteamAPI_Init");
                return;
            }

            default:
                throw new SteamAppTicketException(SteamAppTicketError.InterfaceUnavailable,
                    $"{SteamAppTicketError.InterfaceUnavailable.DefaultMessage(gameName)} " +
                    "(neither SteamAPI_InitFlat nor SteamAPI_Init is exported)");
        }
    }

    private static byte[] Request(NativeSteamApiExports exports, string gameName, TimeSpan timeout,
        ILogger logger, CancellationToken token)
    {
        var user = ResolveInterface(exports, SteamApiInterfaceProbe.UserInterface, gameName, logger);
        var apps = ResolveInterface(exports, SteamApiInterfaceProbe.AppsInterface, gameName, logger);
        var utils = ResolveInterface(exports, SteamApiInterfaceProbe.UtilsInterface, gameName, logger);

        var isSubscribed = exports.GetDelegate<SteamApiNative.BIsSubscribed>(SteamApiNative.IsSubscribedExport);
        if (isSubscribed != null && isSubscribed(apps) == 0)
            throw new SteamAppTicketException(SteamAppTicketError.NotEntitled,
                SteamAppTicketError.NotEntitled.DefaultMessage(gameName));

        var request = exports.GetDelegate<SteamApiNative.RequestEncryptedAppTicket>(
                          SteamApiNative.RequestTicketExport)
                      ?? throw Missing(SteamApiNative.RequestTicketExport, gameName);
        var fetch = exports.GetDelegate<SteamApiNative.GetEncryptedAppTicket>(SteamApiNative.GetTicketExport)
                    ?? throw Missing(SteamApiNative.GetTicketExport, gameName);
        var runCallbacks = exports.GetDelegate<SteamApiNative.RunCallbacks>(SteamApiNative.RunCallbacksExport)
                           ?? throw Missing(SteamApiNative.RunCallbacksExport, gameName);

        var payload = Marshal.AllocHGlobal(TicketPayload.Length);
        var buffer = Marshal.AllocHGlobal(SteamApiNative.TicketBufferSize);
        try
        {
            Marshal.Copy(TicketPayload, 0, payload, TicketPayload.Length);
            var call = request(user, payload, TicketPayload.Length);
            logger.LogDebug("Asked Steam for an encrypted app ticket (call {Call})", call);

            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                token.ThrowIfCancellationRequested();

                runCallbacks();

                if (fetch(user, buffer, SteamApiNative.TicketBufferSize, out var written) != 0 && written > 0)
                {
                    var ticket = new byte[written];
                    Marshal.Copy(buffer, ticket, 0, ticket.Length);
                    return ticket;
                }

                // Only ever adds a reason. Steam may have already reaped the result by the time this asks,
                // in which case nothing is learned and the loop keeps waiting on the ticket itself.
                var refusal = ReadRefusal(exports, utils, call);
                if (refusal != null)
                    throw new SteamAppTicketException(SteamAppTicketError.TicketRefused,
                        $"Steam refused to issue an app ticket for {gameName}: {refusal}.");

                Thread.Sleep(PollInterval);
            }

            throw new SteamAppTicketException(SteamAppTicketError.TicketTimedOut,
                $"{SteamAppTicketError.TicketTimedOut.DefaultMessage(gameName)} " +
                $"(waited {timeout.TotalSeconds:0} seconds)");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(payload);
        }
    }

    private static IntPtr ResolveInterface(NativeSteamApiExports exports, string name, string gameName,
        ILogger logger)
    {
        var handle = SteamApiInterfaceProbe.Resolve(exports, name, out var export);
        if (handle == IntPtr.Zero)
            throw new SteamAppTicketException(SteamAppTicketError.InterfaceUnavailable,
                $"{SteamAppTicketError.InterfaceUnavailable.DefaultMessage(gameName)} " +
                $"(nothing between {SteamApiInterfaceProbe.ExportName(name, SteamApiInterfaceProbe.HighestVersion)} " +
                $"and {SteamApiInterfaceProbe.ExportName(name, SteamApiInterfaceProbe.LowestVersion)} answered)");

        logger.LogDebug("Resolved ISteam{Interface} through {Export}", name, export);
        return handle;
    }

    /// <summary>
    ///     Reads the result Steam posted for the ticket request, when it has posted one and nobody else has
    ///     taken it. Returns null while there is nothing to read, or when the answer was success - in which
    ///     case the ticket is on its way and the caller should carry on waiting for it.
    /// </summary>
    private static string? ReadRefusal(NativeSteamApiExports exports, IntPtr utils, ulong call)
    {
        var completed = exports.GetDelegate<SteamApiNative.IsApiCallCompleted>(
            SteamApiNative.IsApiCallCompletedExport);
        var result = exports.GetDelegate<SteamApiNative.GetApiCallResult>(SteamApiNative.GetApiCallResultExport);
        if (completed == null || result == null) return null;

        if (completed(utils, call, out var pending) == 0 || pending != 0) return null;

        var response = Marshal.AllocHGlobal(SteamApiNative.EncryptedAppTicketResponseSize);
        try
        {
            var read = result(utils, call, response, SteamApiNative.EncryptedAppTicketResponseSize,
                SteamApiNative.EncryptedAppTicketResponseCallback, out var failed);
            if (read == 0 || failed != 0) return null;

            var code = Marshal.ReadInt32(response);
            return code == SteamResultOk ? null : DescribeResult(code);
        }
        finally
        {
            Marshal.FreeHGlobal(response);
        }
    }

    private const int SteamResultOk = 1;

    /// <summary>
    ///     The <c>EResult</c> values this request actually produces, per Steamworks' own documentation for
    ///     <c>RequestEncryptedAppTicket</c>. Anything else is reported as its number, which is still more than
    ///     a silent timeout would have said.
    /// </summary>
    private static readonly Dictionary<int, string> ResultNames = new()
    {
        {3, "Steam has no connection to its servers (offline mode, or the network is down)"},
        {8, "the request was malformed"},
        {15, "access denied"},
        {21, "Steam is busy and asked to be tried again later"},
        {25, "too many ticket requests too quickly; wait a minute"},
        {29, "a ticket request for this app is already in flight"},
        {84, "Steam is rate limiting this account"}
    };

    private static string DescribeResult(int code)
    {
        return ResultNames.TryGetValue(code, out var name) ? name : $"EResult {code}";
    }

    private static SteamAppTicketException Missing(string export, string gameName)
    {
        return new SteamAppTicketException(SteamAppTicketError.InterfaceUnavailable,
            $"{SteamAppTicketError.InterfaceUnavailable.DefaultMessage(gameName)} ({export} is not exported)");
    }

    private static void SafeShutdown(NativeSteamApiExports exports, ILogger logger)
    {
        try
        {
            exports.GetDelegate<SteamApiNative.Shutdown>(SteamApiNative.ShutdownExport)?.Invoke();
        }
        catch (Exception ex)
        {
            // The ticket, if there was one, is already in hand; a library that faults on the way out is not
            // a reason to turn a success into a failure.
            logger.LogDebug(ex, "SteamAPI_Shutdown threw");
        }
    }

    private static string ReadUtf8(byte[] buffer)
    {
        var end = Array.IndexOf(buffer, (byte) 0);
        return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }
}
