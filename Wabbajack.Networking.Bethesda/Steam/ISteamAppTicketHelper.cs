using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <param name="ExitCode">
///     One of <see cref="SteamAppTicketErrors" />'s codes when the helper got far enough to choose one.
/// </param>
public record SteamAppTicketHelperResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
///     Runs the ticket helper and reports what it said.
///     <para>
///         Separate from <see cref="ChildProcessSteamAppTicketSource" /> so that the part deciding what a
///         helper run <em>meant</em> - which exit code is which failure, what a missing ticket line implies,
///         when to give up waiting - can be tested without a game install, a Steam client, or a process.
///     </para>
/// </summary>
public interface ISteamAppTicketHelper
{
    /// <summary>
    ///     Runs the helper to completion and returns its exit code and output. Honours
    ///     <paramref name="token" /> by ending the process, so a caller's deadline is the real bound on how
    ///     long this takes.
    /// </summary>
    Task<SteamAppTicketHelperResult> Run(IReadOnlyList<string> arguments, CancellationToken token);
}
