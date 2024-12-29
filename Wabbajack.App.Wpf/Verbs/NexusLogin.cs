using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Messages;
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

    public async Task<int> Run(CancellationToken token)
    {
        var tcs = new TaskCompletionSource<int>();
        var handler = _services.GetRequiredService<NexusLoginHandler>();
        handler.Closed += (sender, args) => { tcs.TrySetResult(0); };
        ShowBrowserWindow.Send(handler);

        return await tcs.Task;
    }
}