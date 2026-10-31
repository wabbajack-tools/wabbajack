using System;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     How the helper process hands the ticket back: one line, <c>ticket &lt;hex&gt;</c>, on standard output.
///     <para>
///         Hex rather than base64 because that is what Bethesda's API wants the token in anyway, so nothing
///         re-encodes it on the way through. A prefix rather than a bare line because the CLI logs to the
///         console as well as to its log file, so standard output is shared; the prefix is what lets the
///         parent pick the one line that is an answer out of however many are progress.
///     </para>
///     <para>
///         The ticket goes to standard output directly and never through the logger, which would put a
///         credential-shaped blob in a log file that outlives it.
///     </para>
/// </summary>
public static class SteamAppTicketOutput
{
    public const string Prefix = "ticket ";

    public static string Format(ReadOnlySpan<byte> ticket)
    {
        return Prefix + Convert.ToHexString(ticket);
    }

    /// <summary>
    ///     Pulls the ticket out of whatever the helper printed. Reads the last matching line, so a retry that
    ///     printed twice yields the newer answer rather than the older one.
    /// </summary>
    public static bool TryParse(string? output, out byte[] ticket)
    {
        ticket = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(output)) return false;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var hex = trimmed[Prefix.Length..].Trim();
            if (hex.Length == 0) continue;

            try
            {
                ticket = Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                continue;
            }
        }

        return ticket.Length > 0;
    }
}
