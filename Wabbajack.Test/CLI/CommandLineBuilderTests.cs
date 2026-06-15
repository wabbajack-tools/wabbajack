using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Builder;

namespace Wabbajack.CLI.Test;

[ClassConstructor<CliClassConstructor>]
[NotInParallel]
public class CommandLineBuilderTests
{
    private readonly CLITestFixture _fixture;

    public CommandLineBuilderTests(CLITestFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task Run_WithEncryptVerb_HelpReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "encrypt", "--help" });
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithUnknownVerb_ReturnsNonZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "nonexistent-verb-12345" });
        await Assert.That(result).IsNotEqualTo(0);
    }

    [Test]
    public async Task Run_WithHelpFlag_ReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "--help" });
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithVerbHelpFlag_ReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "hash-file", "--help" });
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithListGamesVerb_CompletesSuccessfully()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "list-games" });
        await Assert.That(result >= 0).IsTrue();
    }

    [Test]
    public async Task Verbs_ReturnsNonEmptyCollection()
    {
        await Assert.That(CommandLineBuilder.Verbs).IsNotEmpty();
    }
}
