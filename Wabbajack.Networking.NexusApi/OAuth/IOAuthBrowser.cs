using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Networking.NexusApi;

/// <summary>
///     Hands a URL to whatever the user browses with. A seam rather than a call to <c>Process.Start</c>
///     because it is the one step of a login that leaves this process, and a test has nowhere to send it.
/// </summary>
public interface IOAuthBrowser
{
    /// <summary>
    ///     False when nothing was opened - no default browser, or the shell refused - which is a login that
    ///     will never arrive rather than one still being waited for.
    /// </summary>
    bool Open(Uri uri);
}

/// <summary>Opens the URL with the machine's default browser.</summary>
public class SystemOAuthBrowser : IOAuthBrowser
{
    private readonly ILogger<SystemOAuthBrowser> _logger;

    public SystemOAuthBrowser(ILogger<SystemOAuthBrowser> logger)
    {
        _logger = logger;
    }

    public bool Open(Uri uri)
    {
        try
        {
            // UseShellExecute is what makes this the user's browser rather than an executable of our own.
            Process.Start(new ProcessStartInfo(uri.ToString()) {UseShellExecute = true});
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open {Uri} in a browser", uri);
            return false;
        }
    }
}
