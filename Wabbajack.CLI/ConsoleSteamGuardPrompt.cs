using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Networking.Steam;

namespace Wabbajack.CLI;

/// <summary>
///     Reads Steam Guard codes from the terminal. The CLI has no intervention handler worth the name, so it
///     supplies this in place of the default one.
///     Every read runs on a worker thread. SteamKit calls these from its polling loop, and a blocking
///     <see cref="Console.ReadLine" /> there would stall the session while the user hunts for their phone.
/// </summary>
public class ConsoleSteamGuardPrompt : ISteamGuardPrompt
{
    public Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
    {
        if (previousCodeWasIncorrect)
            Console.WriteLine("That code was not accepted.");

        return ReadAsync("Steam Guard code from the Steam mobile app (blank to cancel): ", token);
    }

    public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
    {
        if (previousCodeWasIncorrect)
            Console.WriteLine("That code was not accepted.");

        return ReadAsync($"Steam Guard code emailed to {email} (blank to cancel): ", token);
    }

    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
    {
        Console.WriteLine("Approve this login in the Steam mobile app.");
        return Task.FromResult(true);
    }

    private static Task<string?> ReadAsync(string prompt, CancellationToken token)
    {
        return Task.Run(() =>
        {
            Console.Write(prompt);
            var line = Console.ReadLine();
            // A blank line, EOF or a cancelled run all mean the same thing: give up on this login.
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(line)) return (string?) null;
            return line.Trim();
        }, token);
    }
}
