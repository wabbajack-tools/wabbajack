using System;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     How long to wait, on both sides of the process boundary. Injected rather than hardcoded so tests do
///     not have to sit through a real one.
/// </summary>
public record SteamAppTicketOptions
{
    /// <summary>
    ///     How long the helper waits for Steam to produce the ticket. Tickets normally arrive in well under a
    ///     second; this is sized for a Steam client that has just started and is still connecting.
    /// </summary>
    public TimeSpan TicketTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     What the parent allows on top of <see cref="TicketTimeout" /> for the helper to start, load a
    ///     native library and exit. A helper that outruns this has wedged rather than been refused, and is
    ///     reported as such.
    /// </summary>
    public TimeSpan HelperGrace { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan HelperTimeout => TicketTimeout + HelperGrace;
}
