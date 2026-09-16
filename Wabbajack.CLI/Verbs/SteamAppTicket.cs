using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Networking.Bethesda;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Asks the running Steam client for an encrypted app ticket and prints it as hex.
///     <para>
///         This is the child half of <see cref="ChildProcessSteamAppTicketSource" />, and the only place in
///         Wabbajack that loads a game's <c>steam_api64.dll</c>. It runs here, in a process that exists for
///         about a second, because initialising Steamworks is process-wide: it makes whatever process does it
///         register with the Steam client as the game - the account shows as playing it for as long as the
///         process lives - and it takes a native library shipped by the game into the address space, where a
///         bad one is a crash rather than an error. Both of those are survivable in a helper and are not in
///         the app.
///     </para>
///     <para>
///         The user does not log in to Wabbajack with Steam for this, and nothing is stored. The Steam client
///         they already have running mints the ticket on behalf of whoever is signed in to it.
///     </para>
///     <para>
///         The ticket goes to standard output through <see cref="SteamAppTicketOutput" /> and never through
///         the logger, which also writes to a file that would then be holding something worth stealing.
///     </para>
/// </summary>
public class SteamAppTicket
{
    private readonly IGameLocator _locator;
    private readonly ILogger<SteamAppTicket> _logger;

    public SteamAppTicket(ILogger<SteamAppTicket> logger, IGameLocator locator)
    {
        _logger = logger;
        _locator = locator;
    }

    public static VerbDefinition Definition = new VerbDefinition(ChildProcessSteamAppTicketSource.Verb,
        "Mints a Steam encrypted app ticket for an app and prints it as hex", new[]
        {
            new OptionDefinition(typeof(uint), "a", "app", "Steam app id to mint a ticket for, e.g. 489830"),
            new OptionDefinition(typeof(AbsolutePath), "f", "folder",
                "Game folder holding steam_api64.dll. Located automatically when omitted"),
            new OptionDefinition(typeof(int), "t", "timeout", "Seconds to wait for Steam to produce the ticket")
        });

    public Task<int> Run(uint app, AbsolutePath folder, int timeout, CancellationToken token)
    {
        if (app == 0)
        {
            _logger.LogError("--app is required: say which Steam app to mint a ticket for");
            return Task.FromResult(1);
        }

        try
        {
            return Task.FromResult(Mint(app, folder, timeout, token));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cancelled");
            return Task.FromResult(1);
        }
        catch (SteamAppTicketException ex)
        {
            // Standard error, so the parent gets the specific sentence rather than having to fall back on
            // the generic one the exit code maps to.
            Console.Error.WriteLine(ex.Message);
            _logger.LogError("{Message}", ex.Message);
            return Task.FromResult(ex.Error.ToExitCode());
        }
    }

    private int Mint(uint app, AbsolutePath folder, int timeout, CancellationToken token)
    {
        var name = SteamAppGames.Describe(app);

        if (folder == default)
        {
            // Only a game Wabbajack knows can be located; anything else has to be pointed at its folder,
            // which is what the caller that already looked does.
            var metaData = SteamAppGames.ForAppId(app)
                           ?? throw new SteamAppTicketException(SteamAppTicketError.GameNotFound,
                               $"Wabbajack does not know a game with Steam app id {app}, so --folder has to say " +
                               "where its copy of steam_api64.dll is.");

            if (!_locator.TryFindLocation(metaData.Game, out folder))
                throw new SteamAppTicketException(SteamAppTicketError.GameNotFound,
                    SteamAppTicketError.GameNotFound.DefaultMessage(name));
        }

        var seconds = timeout > 0 ? timeout : (int) new SteamAppTicketOptions().TicketTimeout.TotalSeconds;

        _logger.LogInformation("Asking Steam for an app ticket for {Game} (app {App}) using {Folder}", name, app,
            folder);

        var ticket = SteamAppTicketMinter.Mint(folder, app, name, TimeSpan.FromSeconds(seconds), _logger, token);

        Console.Out.WriteLine(SteamAppTicketOutput.Format(ticket));
        Console.Out.Flush();

        _logger.LogInformation("Steam issued a {Bytes} byte app ticket", ticket.Length);
        return 0;
    }
}
