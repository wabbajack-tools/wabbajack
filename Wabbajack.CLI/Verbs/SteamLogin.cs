using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Networking.Steam;

namespace Wabbajack.CLI.Verbs;

public class SteamLogin
{
    private readonly ILogger<SteamLogin> _logger;
    private readonly ISteamSession _session;

    public SteamLogin(ILogger<SteamLogin> logger, ISteamSession session)
    {
        _logger = logger;
        _session = session;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-login",
        "Logs into Steam and saves the login for later runs",
        new[]
        {
            new OptionDefinition(typeof(bool), "q", "qr",
                "Log in by scanning a QR code with the Steam mobile app. The default when no username is given"),
            new OptionDefinition(typeof(string), "u", "username",
                "Steam account name, to log in with a password instead of a QR code"),
            new OptionDefinition(typeof(bool), "f", "force",
                "Ignore any saved login and authenticate from scratch"),
            new OptionDefinition(typeof(bool), "i", "invert",
                "Draw the QR code inverted, for a terminal with light text on a dark background")
        });

    public async Task<int> Run(bool qr, string username, bool force, bool invert, CancellationToken token)
    {
        if (!force && _session.HaveStoredToken)
            try
            {
                var reused = await _session.LoginWithStoredTokenAsync(token);
                Report(reused);
                return 0;
            }
            catch (SteamLoginRequiredException ex)
            {
                _logger.LogInformation("{Message}", ex.Message);
            }

        var useQr = qr || string.IsNullOrWhiteSpace(username);

        try
        {
            var result = useQr
                ? await LoginWithQrCode(invert, token)
                : await LoginWithCredentials(username, token);
            Report(result);
            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Steam login cancelled");
            return 1;
        }
        catch (SteamCredentialsRejectedException)
        {
            // The one place a password error is the right thing to say. An expired token never lands here.
            _logger.LogError("Steam did not accept that account name and password");
            return 1;
        }
        catch (SteamLoginRequiredException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return 1;
        }
        catch (SteamLoginInProgressException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return 1;
        }
        catch (SteamException ex)
        {
            _logger.LogError("Steam refused the login: {Result}", ex.Result);
            return 1;
        }
        catch (TimeoutException)
        {
            _logger.LogError("Steam did not answer in time. Check your connection and try again");
            return 1;
        }
    }

    private async Task<SteamLoginResult> LoginWithQrCode(bool invert, CancellationToken token)
    {
        Console.WriteLine("Open the Steam mobile app, go to the Steam Guard screen and scan this code.");
        Console.WriteLine("This flow needs the mobile authenticator; use --username to log in with a password instead.");
        Console.WriteLine("If your terminal draws light text on a dark background, re-run with --invert.");

        // Steam rotates the URL every few seconds as a side effect of polling, and calls this back from the
        // polling thread. Drawing is all this does, and Console serialises its own writes, so there is
        // nothing here to marshal.
        return await _session.LoginWithQrCodeAsync(url =>
        {
            Console.WriteLine();
            Console.Write(ConsoleQrCode.Render(url, invert));
            // The URL is printed too, so a terminal that mangles block characters still has a way through.
            Console.WriteLine(url);
        }, token);
    }

    private async Task<SteamLoginResult> LoginWithCredentials(string username, CancellationToken token)
    {
        var password = ReadPassword($"Password for {username}: ");
        if (string.IsNullOrEmpty(password))
        {
            _logger.LogInformation("No password given, Steam login cancelled");
            throw new OperationCanceledException();
        }

        return await _session.LoginWithCredentialsAsync(username, password, token);
    }

    private void Report(SteamLoginResult result)
    {
        if (result.UsedStoredToken)
            _logger.LogInformation("Logged into Steam as {AccountName} ({SteamId}) with the saved login",
                result.AccountName, result.SteamId);
        else
            _logger.LogInformation("Logged into Steam as {AccountName} ({SteamId}); the login has been saved",
                result.AccountName, result.SteamId);
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            // Nothing to mask when the input is piped in.
            var piped = Console.ReadLine();
            return piped ?? string.Empty;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return builder.ToString();
                case ConsoleKey.Escape:
                    Console.WriteLine();
                    return string.Empty;
                case ConsoleKey.Backspace:
                    if (builder.Length > 0) builder.Length--;
                    break;
                default:
                    if (!char.IsControl(key.KeyChar)) builder.Append(key.KeyChar);
                    break;
            }
        }
    }
}
