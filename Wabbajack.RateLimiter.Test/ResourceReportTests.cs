using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.RateLimiter.Test;

/// <summary>
///     <see cref="Resource{T}.Report" /> does two jobs: it records bytes against the resource, and it paces
///     the caller when a throughput cap is set. Only the second needs the pump - a bounded channel, one
///     consumer task and a <see cref="TaskCompletionSource" /> per report - and almost nothing sets a cap.
///     <c>HashingCopy</c> calls this once per megabyte read, on every hashing thread at once, so an uncapped
///     resource taking that round-trip cost between an eighth and a third of the throughput of hashing a
///     game folder.
/// </summary>
public class ResourceReportTests
{
    private sealed class TestResource
    {
    }

    /// <summary>
    ///     The pin: uncapped, a report completes without yielding. Asserted on the ValueTask rather than on
    ///     a stopwatch, so nothing here depends on how loaded the machine is.
    /// </summary>
    [Fact]
    public async Task AnUncappedReportDoesNotGoThroughThePump()
    {
        foreach (var uncapped in new[] {long.MaxValue, 0L})
        {
            var resource = new Resource<TestResource>("Test", 1, uncapped);
            using var job = await resource.Begin("Reporting", 1024, CancellationToken.None);

            var reported = resource.Report(job, 1024, CancellationToken.None);

            Assert.True(reported.IsCompletedSuccessfully, $"MaxThroughput {uncapped} still awaited the pump");
            await reported;
            Assert.Equal(1024, resource.StatusReport.Transferred);
        }
    }

    /// <summary>The bytes still add up, which is what the throughput readout in the UI is drawn from.</summary>
    [Fact]
    public async Task AnUncappedReportStillRecordsWhatWasTransferred()
    {
        var resource = new Resource<TestResource>("Test", 4);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var job = await resource.Begin("Reporting", 1000, CancellationToken.None);
            for (var i = 0; i < 1000; i++)
                await job.Report(10, CancellationToken.None);
            Assert.Equal(10000, job.Current);
        }));

        Assert.Equal(40000, resource.StatusReport.Transferred);
    }

    /// <summary>
    ///     And a cap is still a cap: two megabytes through a one-megabyte-a-second resource cannot finish in
    ///     under a second. Only the floor is asserted - a machine under load may take much longer, and that
    ///     is not a failure.
    /// </summary>
    [Fact]
    public async Task ACapStillPacesTheCaller()
    {
        const int mib = 1024 * 1024;
        var resource = new Resource<TestResource>("Test", 1, mib);
        using var job = await resource.Begin("Transferring", 2 * mib, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 4; i++)
            await job.Report(mib / 2, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed > TimeSpan.FromSeconds(1),
            $"2MiB at 1MiB/s took {sw.Elapsed.TotalSeconds:F2}s, so the cap was not applied");
        Assert.Equal(2 * mib, resource.StatusReport.Transferred);
    }
}
