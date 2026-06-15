using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.CLI.Test;

/// <summary>
/// Builds the CLI test DI host once (via <see cref="CLITestFixture"/>) and shares it across CLI
/// tests, replacing the old xUnit collection fixture. CLI test classes that need the host take a
/// <see cref="CLITestFixture"/> constructor parameter; they should also be marked
/// <c>[NotInParallel]</c> because they mutate the static <c>CommandLineBuilder</c> command list.
/// </summary>
public sealed class CliClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<CLITestFixture>();
    }
}
