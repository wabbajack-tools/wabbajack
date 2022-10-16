using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Verbs;
using Wabbajack.CLI.Builder;

namespace Wabbajack;

public static class CommandLineBuilderExtensions
{
    public static void AddCLIVerbs(this IServiceCollection services)
    {
        CommandLineBuilder.RegisterCommand<NexusLogin>(NexusLogin.Definition, c => ((NexusLogin)c).Run);
        services.AddSingleton<NexusLogin>();
    }
}
