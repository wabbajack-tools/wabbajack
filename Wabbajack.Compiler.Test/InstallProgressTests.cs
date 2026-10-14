using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Installer;
using Xunit;

namespace Wabbajack.Compiler.Test;

/// <summary>
///     What an install tells the app while it runs. "Installing files" can be most of an install, so it has to
///     report as it goes: the byte count in its status text, and an overall figure that moves with it. The app's
///     progress bar used the step number alone and stood still for the whole step.
/// </summary>
public class InstallProgressTests : IAsyncLifetime
{
    private readonly ModListHarness _harness;
    private readonly IServiceScope _scope;

    public InstallProgressTests(IServiceProvider serviceProvider)
    {
        _scope = serviceProvider.CreateScope();
        _harness = _scope.ServiceProvider.GetService<ModListHarness>()!;
    }

    public async Task InitializeAsync()
    {
        await _harness.InstallGeneratedMod();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InstallingFilesReportsProgressThroughTheStep()
    {
        Assert.NotNull(await _harness.Compile());

        var updates = new ConcurrentQueue<StatusUpdate>();
        Assert.Equal(InstallResult.Succeeded,
            await _harness.InstallWithResult(configureInstaller: i => i.OnStatusUpdate = updates.Enqueue));

        var step = updates.Where(u => u.StatusText.Contains("Installing files")).ToArray();
        Assert.True(step.Length > 2, $"only {step.Length} updates during Installing files");
        Assert.True(step.Max(u => u.StepProgress.Value) > 0.99, "the step never reached its end");

        // The text counts files as well as bytes, and its count reaches the total.
        var counts = step.Select(u => Regex.Match(u.StatusText, @"\(([\d,]+)/([\d,]+) files, "))
            .Where(m => m.Success)
            .Select(m => (Done: int.Parse(m.Groups[1].Value.Replace(",", "")),
                Total: int.Parse(m.Groups[2].Value.Replace(",", ""))))
            .ToArray();
        Assert.NotEmpty(counts);
        Assert.Contains(counts, c => c.Done == c.Total);

        var overall = step.Select(u => u.OverallProgress.Value).ToArray();
        Assert.True(overall.Max() > overall.Min(), "the overall progress did not move during the step");
        Assert.All(step, u => Assert.True(u.OverallProgress.Value <= u.StepsProgress.Value + 1e-9));
    }

    [Theory]
    [InlineData(1, 10, 0.0, 0.0)]
    [InlineData(1, 10, 0.5, 0.05)]
    [InlineData(10, 15, 0.0, 9.0 / 15)]
    [InlineData(10, 15, 0.5, 9.5 / 15)]
    [InlineData(10, 15, 1.0, 10.0 / 15)]
    public void OverallProgressIsTheStepsDoneAndThePartOfThisOne(int step, int steps, double stepProgress,
        double expected)
    {
        var update = new StatusUpdate("", "", RateLimiter.Percent.FactoryPutInRange(step, steps),
            RateLimiter.Percent.FactoryPutInRange(stepProgress), step);

        Assert.Equal(expected, update.OverallProgress.Value, 6);
    }
}
