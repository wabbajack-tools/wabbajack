using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     Gets a ticket by running <c>wabbajack-cli steam-app-ticket</c> and reading what it prints.
///     <para>
///         Everything native happens over there and for about a second. Here there is an app id, a game
///         folder, a process, an exit code and a line of hex.
///     </para>
/// </summary>
public class ChildProcessSteamAppTicketSource : ISteamAppTicketSource
{
    /// <summary>The verb the helper runs. Named here because both sides have to agree on it.</summary>
    public const string Verb = "steam-app-ticket";

    private readonly ISteamAppTicketHelper _helper;
    private readonly IGameLocator _locator;
    private readonly ILogger<ChildProcessSteamAppTicketSource> _logger;
    private readonly SteamAppTicketOptions _options;

    public ChildProcessSteamAppTicketSource(ILogger<ChildProcessSteamAppTicketSource> logger, IGameLocator locator,
        ISteamAppTicketHelper helper, SteamAppTicketOptions options)
    {
        _logger = logger;
        _locator = locator;
        _helper = helper;
        _options = options;
    }

    public async ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default)
    {
        var name = SteamAppGames.Describe(appId);

        // The app id says which app to speak for; the folder is where the library that does the speaking
        // lives, and only a game Wabbajack knows has one it can find.
        var metaData = SteamAppGames.ForAppId(appId)
                       ?? throw new SteamAppTicketException(SteamAppTicketError.GameNotFound,
                           $"Wabbajack does not know a game with Steam app id {appId}, so it cannot find the " +
                           "copy of steam_api64.dll that would ask Steam for a ticket.");

        // Located here rather than left to the helper so that the two failures the user can do something
        // about - the game is not installed, the library is not in it - are answered before a process is
        // started, and answered naming the folder that was looked in.
        if (!_locator.TryFindLocation(metaData.Game, out var folder))
            throw new SteamAppTicketException(SteamAppTicketError.GameNotFound,
                SteamAppTicketError.GameNotFound.DefaultMessage(name));

        var library = folder.Combine(SteamAppTicketMinter.LibraryName);
        if (!library.FileExists())
            throw new SteamAppTicketException(SteamAppTicketError.SteamApiMissing,
                $"{SteamAppTicketError.SteamApiMissing.DefaultMessage(name)} (looked for {library})");

        var result = await Run(Arguments(appId, folder), name, token);

        if (result.ExitCode != 0) throw Failed(result, name);

        if (!SteamAppTicketOutput.TryParse(result.StandardOutput, out var ticket))
            throw new SteamAppTicketException(SteamAppTicketError.HelperFailed,
                $"The Steam app ticket helper said it succeeded and printed no ticket.{Detail(result)}");

        _logger.LogInformation("Steam issued a {Bytes} byte app ticket for {Game}", ticket.Length, name);
        return ticket;
    }

    private IReadOnlyList<string> Arguments(uint appId, AbsolutePath folder)
    {
        return new[]
        {
            Verb,
            "--app", appId.ToString(CultureInfo.InvariantCulture),
            "--folder", folder.ToString(),
            "--timeout",
            ((int) Math.Ceiling(_options.TicketTimeout.TotalSeconds)).ToString(CultureInfo.InvariantCulture)
        };
    }

    private async Task<SteamAppTicketHelperResult> Run(IReadOnlyList<string> arguments, string name,
        CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_options.HelperTimeout);

        try
        {
            return await _helper.Run(arguments, deadline.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The caller did not cancel, so this is the grace period running out: the helper is stuck
            // somewhere other than waiting on Steam, which has its own shorter deadline and its own code.
            throw new SteamAppTicketException(SteamAppTicketError.HelperFailed,
                $"The Steam app ticket helper did not finish within {_options.HelperTimeout.TotalSeconds:0} " +
                $"seconds while asking about {name}, so it was ended.");
        }
    }

    private static SteamAppTicketException Failed(SteamAppTicketHelperResult result, string name)
    {
        var error = SteamAppTicketErrors.FromExitCode(result.ExitCode);
        var detail = result.StandardError.Trim();

        // The helper's own sentence is the better one - it knows which interface, which Steam result, which
        // folder, and it already says the general thing as well - so it is used whole when there is one. The
        // generic sentence for the code fills in when the helper died before it could say anything.
        return new SteamAppTicketException(error, detail.Length > 0 ? detail : error.DefaultMessage(name));
    }

    private static string Detail(SteamAppTicketHelperResult result)
    {
        var text = result.StandardError.Trim();
        return text.Length == 0 ? string.Empty : Environment.NewLine + text;
    }
}
