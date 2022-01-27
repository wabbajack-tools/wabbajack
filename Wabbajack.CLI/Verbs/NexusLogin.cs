using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Browser;

namespace Wabbajack.CLI.Verbs;

public class NexusLogin : AVerb
{
    private readonly ILogger<NexusLogin> _logger;
    private readonly BrowserHost _host;

    public NexusLogin(ILogger<NexusLogin> logger, BrowserHost host)
    {
        _logger = logger;
        _host = host;
    }
    
    public static Command MakeCommand()
    {
        var command = new Command("nexus-login");
        command.Description = "Prompt the user to log into the nexus";
        return command;
    }
    
    public async Task<int> Run(CancellationToken token)
    {
        var browser = await _host.CreateBrowser();

        return 0;
    }

    protected override ICommandHandler GetHandler()
    {
        return CommandHandler.Create(Run);
    }
}