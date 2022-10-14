
using Microsoft.Extensions.DependencyInjection;
namespace Wabbajack;
using Wabbajack.Verbs;
using Wabbajack.CLI.Builder;

public static class CommandLineBuilderExtensions{ 

public static void AddCLIVerbs(this IServiceCollection services) {
CommandLineBuilder.RegisterCommand<NexusLogin>(NexusLogin.Definition, c => ((NexusLogin)c).Run);
services.AddSingleton<NexusLogin>();
}
}