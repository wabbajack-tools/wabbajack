using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.LoginManagers;

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

    public async Task<int> Run(CancellationToken token)
    {
        var manager = _services.GetRequiredService<NexusLoginManager>();
        manager.TriggerLogin.Execute(null);

        // Give the async login flow time to complete
        // The command will return once the user completes login in their browser
        await Task.Delay(TimeSpan.FromMinutes(5), token);
        return 0;
    }
}
