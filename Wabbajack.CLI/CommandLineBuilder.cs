using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Verbs;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI;

public partial class CommandLineBuilder
{
    private readonly IConsole _console;
    private readonly IEnumerable<IVerb> _verbs;

    public CommandLineBuilder(IEnumerable<IVerb> verbs, IConsole console, LoggingRateLimiterReporter _)
    {
        _console = console;
        _verbs = verbs;
    }

    static CommandLineBuilder()
    {
        RegisterAll();
    }

    public async Task<int> Run(string[] args)
    {
        var root = new RootCommand();
        foreach (var verb in _verbs)
            root.Add(verb.MakeCommand());
        
        return await root.InvokeAsync(args);
    }

    private static List<(Type, Func<Command>)> _commands { get; set; } = new();
    public static IEnumerable<Type> Verbs => _commands.Select(c => c.Item1);
    public static void RegisterCommand<T>(Func<Command> makeCommand)
    {
        _commands.Add((typeof(T), makeCommand));
        
    }
}

public record VerbDefinition()
{
    
}

public static class CommandLineBuilderExtensions
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddSingleton<CommandLineBuilder>();

        foreach (var verb in CommandLineBuilder.Verbs)
        {
            services.AddSingleton(typeof(IVerb), verb);
        }

        return services;
    }
}