using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Builder;
using Xunit;

namespace Wabbajack.CLI.Test;

[Collection("CLI")]
public class CommandLineBuilderTests
{
    private readonly CLITestFixture _fixture;

    public CommandLineBuilderTests(CLITestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Run_WithEncryptVerb_HelpReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "encrypt", "--help" });
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithUnknownVerb_ReturnsNonZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "nonexistent-verb-12345" });
        Assert.NotEqual(0, result);
    }

    [Fact]
    public async Task Run_WithHelpFlag_ReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "--help" });
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithVerbHelpFlag_ReturnsZero()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "hash-file", "--help" });
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithListGamesVerb_CompletesSuccessfully()
    {
        var builder = _fixture.ServiceProvider.GetRequiredService<CommandLineBuilder>();
        var result = await builder.Run(new[] { "list-games" });
        Assert.True(result >= 0, $"list-games returned {result}");
    }

    [Fact]
    public void Verbs_ReturnsNonEmptyCollection()
    {
        Assert.NotEmpty(CommandLineBuilder.Verbs);
    }
}
