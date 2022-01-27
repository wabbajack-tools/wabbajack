using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Wabbajack.CLI.Verbs;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI;

public class CommandLineBuilder
{
    private readonly IConsole _console;
    private readonly VerbRegistrar _verbs;
    private readonly IServiceProvider _serviceProvider;

    public CommandLineBuilder(VerbRegistrar verbs, IConsole console, LoggingRateLimiterReporter _, IServiceProvider serviceProvider)
    {
        _console = console;
        _verbs = verbs;
        _serviceProvider = serviceProvider;
    }

    public async Task<int> Run(string[] args)
    {
        var root = new RootCommand();
        foreach (var verb in _verbs.Definitions)
        {
            var command = verb.MakeCommand();
            command.Handler = AVerb.WrapHandler(verb.VerbType, _serviceProvider);
            root.Add(command);
        }

        return await root.InvokeAsync(args);
    }
}