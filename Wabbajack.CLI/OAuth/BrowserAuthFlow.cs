using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wabbajack.CLI.OAuth;

public class BrowserAuthFlow : INexusOAuthFlow
{
    private readonly ILogger<BrowserAuthFlow> _logger;

    public BrowserAuthFlow(ILogger<BrowserAuthFlow> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAuthorizationCode(Uri authorizeUrl, string expectedState, CancellationToken token)
    {
        // AbsoluteUri preserves percent-encoding; ToString() decodes %20 back to spaces
        var urlString = authorizeUrl.AbsoluteUri;

        Console.WriteLine();
        Console.WriteLine("Nexus Mods login");
        Console.WriteLine();
        Console.WriteLine("Your browser will open to the Nexus login page. After logging in:");
        Console.WriteLine("  1. Nexus will redirect your browser to a URL starting with https://127.0.0.1:1234");
        Console.WriteLine("  2. The page will show a connection error — this is expected.");
        Console.WriteLine("  3. Copy the full URL from your browser's address bar and paste it below.");
        Console.WriteLine();
        Console.Write("Press Enter to open your browser...");

        try
        {
            await Task.Run(() => Console.ReadLine(), token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        TryOpenBrowser(urlString);
        _logger.LogInformation("Browser opened. Waiting for you to complete login...");
        Console.WriteLine();
        Console.Write("Paste the redirect URL: ");

        string? input;
        try
        {
            input = await Task.Run(() => Console.ReadLine(), token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogError("No URL entered");
            return null;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var callbackUri))
        {
            _logger.LogError("Invalid URL entered");
            return null;
        }

        var query = ParseQuery(callbackUri.Query);

        if (query.TryGetValue("error", out var error))
        {
            _logger.LogError("OAuth error: {Error}", error);
            return null;
        }

        if (!query.TryGetValue("state", out var returnedState) || returnedState != expectedState)
        {
            _logger.LogError("OAuth state mismatch — the URL may be invalid or from a different login attempt");
            return null;
        }

        if (!query.TryGetValue("code", out var code))
        {
            _logger.LogError("No authorization code found in URL");
            return null;
        }

        return code;
    }

    private void TryOpenBrowser(string url)
    {
        try
        {
            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(url);
            }
            else
            {
                psi = new ProcessStartInfo(url) { UseShellExecute = true };
            }
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not open browser automatically");
        }
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0]),
                p => Uri.UnescapeDataString(p[1]));
}
