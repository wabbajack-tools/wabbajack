using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Verbs;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI;

public partial class CommandLineBuilder
{
    private readonly IConsole _console;
    private readonly IEnumerable<IVerb> _verbs;
    private static IServiceProvider _provider;

    public CommandLineBuilder(IServiceProvider provider, IConsole console, LoggingRateLimiterReporter _)
    {
        _provider = provider;
    }

    static CommandLineBuilder()
    {
        RegisterAll();
    }

    public async Task<int> Run(string[] args)
    {
        var root = new RootCommand();
        foreach (var verb in _commands)
        {
            root.Add(MakeCommend(verb.Type, verb.Handler, verb.Definition));
        }

        return await root.InvokeAsync(args);
    }

    private static Dictionary<Type, Func<OptionDefinition, Option>> _optionCtors = new()
    {
        {
            typeof(string),
            d => new Option<string>(d.Aliases, description: d.Description)
        },
        {
            typeof(AbsolutePath),
            d => new Option<AbsolutePath>(d.Aliases, description: d.Description, parseArgument: d => d.Tokens.Single().Value.ToAbsolutePath())
        },
        {
            typeof(Uri),
            d => new Option<Uri>(d.Aliases, description: d.Description)
        },
        {
            typeof(bool),
            d => new Option<bool>(d.Aliases, description: d.Description)
        },
        
    };

    private Command MakeCommend(Type verbType, Func<IVerb, Delegate> verbHandler, VerbDefinition definition)
    {
        var command = new Command(definition.Name, definition.Description);
        foreach (var option in definition.Options)
        {
            command.Add(_optionCtors[option.Type](option));
        }
        command.Handler = new HandlerDelegate(_provider, verbType, verbHandler);
        return command;
    }
    
    private class HandlerDelegate : ICommandHandler
    {
        private IServiceProvider _provider;
        private Type _type;
        private readonly Func<IVerb, Delegate> _delgate;

        public HandlerDelegate(IServiceProvider provider, Type type, Func<IVerb, Delegate> inner)
        {
            _provider = provider;
            _type = type;
            _delgate = inner;
        }
        public int Invoke(InvocationContext context)
        {
            var service = (IVerb)_provider.GetRequiredService(_type);
            var handler = CommandHandler.Create(_delgate(service));
            return handler.Invoke(context);
        }

        public Task<int> InvokeAsync(InvocationContext context)
        {
            var service = (IVerb)_provider.GetRequiredService(_type);
            var handler = CommandHandler.Create(_delgate(service));
            return handler.InvokeAsync(context);
        }
    }

    private static List<(Type Type, VerbDefinition Definition, Func<IVerb, Delegate> Handler)> _commands { get; set; } = new();
    public static IEnumerable<Type> Verbs => _commands.Select(c => c.Type);
    public static void RegisterCommand<T>(VerbDefinition definition, Func<IVerb, Delegate> handler)
    {
        _commands.Add((typeof(T), definition, handler));
        
    }
}

public record OptionDefinition(Type Type, string ShortOption, string LongOption, string Description)
{
    public string[] Aliases
    {
        get
        {
            return new[] { "-" + ShortOption, "--" + LongOption };
        }
    } 
}

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
            services.AddSingleton(verb);
        }

        return services;
    }
}