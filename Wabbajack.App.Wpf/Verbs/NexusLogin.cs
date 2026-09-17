using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.UserIntervention;

namespace Wabbajack.Verbs;

public class NexusLogin
{
    private readonly ILogger<NexusLogin> _logger;
    private readonly IServiceProvider _services;

    public NexusLogin(ILogger<NexusLogin> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    public static VerbDefinition Definition = new("nexus-login", "Log into the Nexus via the normal browser method",
        Array.Empty<OptionDefinition>());

    /// <summary>
    ///     Opens the login in the user's own browser and waits for the redirect to come back to this
    ///     process. Nothing to close and nothing to wait on but the login itself, so this is just the await.
    /// </summary>
    public async Task<int> Run(CancellationToken token)
    {
        var handler = _services.GetRequiredService<NexusLoginHandler>();
        if (await handler.LogIn(token)) return 0;

        _logger.LogWarning("No Nexus Mods login was stored");
        return 1;
    }
}
