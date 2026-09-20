using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Verbs;

public class NexusLogin
{
    private readonly ILogger<NexusLogin> _logger;
    private readonly NexusOAuthLogin _login;

    public NexusLogin(ILogger<NexusLogin> logger, NexusOAuthLogin login)
    {
        _logger = logger;
        _login = login;
    }

    public static VerbDefinition Definition = new("nexus-login", "Log into the Nexus via the normal browser method",
        Array.Empty<OptionDefinition>());

    /// <summary>
    ///     Waits on the login rather than on a window of ours being closed: the browser is the user's own,
    ///     and this process has nothing on screen to close. A failure is a non-zero exit rather than a throw,
    ///     since every reason a login ends without one is already logged where it happened.
    /// </summary>
    public async Task<int> Run(CancellationToken token)
    {
        // A login nobody finishes must not hang the process: there is no window of ours for the user to
        // close, so the loopback port would otherwise stay open until they killed it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));

        try
        {
            if (await _login.Login(timeout.Token)) return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Gave up waiting for the Nexus Mods login to come back from the browser");
            return 1;
        }

        _logger.LogError("Could not log in to Nexus Mods");
        return 1;
    }
}
