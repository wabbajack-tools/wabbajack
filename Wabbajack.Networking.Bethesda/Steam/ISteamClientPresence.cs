namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     Whether a Steam client is running on this machine.
///     <para>
///         Asked before anything is offered to the user, because the answer decides whether a whole path is
///         available: the encrypted app ticket this chain runs on is minted by the Steam client itself, so
///         with no client there is nothing to ask. A seam rather than a direct call so that a status can be
///         tested without a Steam client, and so nothing outside this namespace has to know the question is
///         answered by looking for a process.
///     </para>
/// </summary>
public interface ISteamClientPresence
{
    bool IsRunning { get; }
}

/// <summary>The real answer: is there a <c>steam</c> process.</summary>
public sealed class SteamClientPresence : ISteamClientPresence
{
    public bool IsRunning => SteamAppTicketMinter.IsSteamClientRunning();
}
