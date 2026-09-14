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

    /// <summary>
    ///     Always waits for the mobile app. Declining would fall back to a typed code, which is a choice the
    ///     console could offer, but it would have to be offered before the approval is already pending.
    /// </summary>
    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
    {
        Console.WriteLine("Approve this login in the Steam mobile app.");
        return Task.FromResult(true);
    }

    private static async Task<string?> ReadAsync(string prompt, CancellationToken token)
    {
        Console.Write(prompt);

        // Console.ReadLine cannot be interrupted, so cancelling unblocks the caller while this worker stays
        // parked until the user presses Enter. In a console process that is on its way out anyway that is
        // the cheapest honest answer; doing better means reading the console key by key.
        var read = Task.Run(Console.ReadLine, CancellationToken.None);
        _ = read.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

        try
        {
            var line = await read.WaitAsync(token).ConfigureAwait(false);
            // A blank line and EOF mean the same thing as cancelling: give up on this login.
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
