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

    public async Task<int> Run(CancellationToken token)
    {
        var tcs = new TaskCompletionSource<int>();
        var view = new BrowserWindow(_services);
        view.Closed += (sender, args) => { tcs.TrySetResult(0); };
        var provider = _services.GetRequiredService<NexusLoginHandler>();
        view.DataContext = provider;
        view.Show();

        return await tcs.Task;
    }
}