using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Wabbajack.CLI.Verbs;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI;

public class CommandLineBuilder
{
    private readonly IConsole _console;
    private readonly IEnumerable<IVerb> _verbs;

    public CommandLineBuilder(IEnumerable<IVerb> verbs, IConsole console, LoggingRateLimiterReporter _)
    {
        _console = console;
        _verbs = verbs;
    }

    public async Task<int> Run(string[] args)
    {
        var root = new RootCommand();
        foreach (var verb in _verbs)
            root.Add(verb.MakeCommand());
        
        return await root.InvokeAsync(args);
    }
}