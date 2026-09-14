using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     A check whose behaviour the test scripts. Counts runs and how many were in flight at once.
/// </summary>
public sealed class FakeCheck : IPreflightCheck
{
    private int _concurrent;

    public FakeCheck(string id, int order, params string[] dependsOn)
    {
        Id = id;
        Order = order;
        DependsOn = dependsOn;
        Title = id;
    }

    public Func<PreflightContext, IPreflightProgress, CancellationToken, Task<PreflightResult>> Body { get; set; } =
        (_, _, _) => Task.FromResult(PreflightResult.Passed("ok"));

    public int Runs { get; private set; }
    public int MaxConcurrent { get; private set; }

    public string Id { get; }
    public string Title { get; }
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }

    /// <summary>Defaults to the interface's answer; the download checks are the ones that say false.</summary>
    public bool NeedsUserStopsRun { get; set; } = true;

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        Runs++;
        var now = Interlocked.Increment(ref _concurrent);
        MaxConcurrent = Math.Max(MaxConcurrent, now);
        try
        {
            return await Body(ctx, progress, token);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrent);
        }
    }
}
