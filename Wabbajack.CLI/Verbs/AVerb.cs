using System;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack.CLI.Verbs;

public abstract class AVerb
{
    public static ICommandHandler WrapHandler(Type type, IServiceProvider provider)
    {
        return new WrappedHandler(type, provider);
    }
    protected abstract ICommandHandler GetHandler();

    private class WrappedHandler : ICommandHandler
    {
        private readonly IServiceProvider _provider;
        private readonly Type _type;

        public WrappedHandler(Type type, IServiceProvider provider)
        {
            _provider = provider;
            _type = type;
        }
        public Task<int> InvokeAsync(InvocationContext context)
        {
            var verb = (AVerb)_provider.GetRequiredService(_type);
            return verb.GetHandler().InvokeAsync(context);
        }
    }
}