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

    private static List<(Type Type, VerbDefinition Definition, Func<IVerb, Delegate> Handler)> _commands { get; set; } = new();
    public static IEnumerable<Type> Verbs => _commands.Select(c => c.Type);
    public static void RegisterCommand<T>(VerbDefinition definition, Func<IVerb, Delegate> handler)
    {
        _commands.Add((typeof(T), definition, handler));
        
    }
}
public record OptionDefinition(Type type, string ShortOption, string LongOption, string Description);

public record VerbDefinition(string Name, string Description, OptionDefinition[] Options)
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