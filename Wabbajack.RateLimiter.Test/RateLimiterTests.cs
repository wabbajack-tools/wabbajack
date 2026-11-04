using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.RateLimiter.Test;

/// <summary>
///     <see cref="Resource{T}" /> is the limiter behind every hashing, copying, extracting and downloading
///     path in the product, so what it promises is worth pinning exactly.
///     <para>
///         These tests deliberately never assert "this took between X and Y seconds". CI runs on a shared,
///         noisy two-core box where a wall clock measures the runner's load rather than the limiter's
///         behaviour. Two shapes are used instead:
///     </para>
///     <list type="bullet">
///         <item>
///             Invariants (never admit more than the limit, never lose or double-count a report) are
///             deterministic and asserted exactly.
///         </item>
///         <item>
///             Liveness (the limit <em>can</em> be reached, a cap <em>does</em> pace the caller) is forced
///             with a rendezvous rather than hoped for from the scheduler, and every timing assertion is
///             one-sided: either "this completes inside a very generous timeout" or "with a delay configured
///             in the hundreds of seconds, this has not completed after a moment". Neither can flip because
///             the machine is slow or has one usable core.
///         </item>
///     </list>
/// </summary>
public class ResourceTests
{
    /// <summary>
    ///     Long enough that only a hang or a genuine deadlock can exhaust it, even on a loaded runner.
    /// </summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Used only to show something has <em>not</em> happened yet, always against work the limiter has
    ///     been told to delay for a hundred seconds or more. A slow machine makes these tests more true,
    ///     never less.
    /// </summary>
    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(250);

    private sealed class TestResource
    {
    }

    private static Resource<TestResource> Limited(int maxTasks)
    {
        return new Resource<TestResource>("Test", maxTasks);
    }

    private static Resource<TestResource> Throttled(int maxTasks, long bytesPerSecond)
    {
        return new Resource<TestResource>("Test", maxTasks, bytesPerSecond);
    }

    private static async Task<bool> CompletedWithin(Task task, TimeSpan window)
    {
        return await Task.WhenAny(task, Task.Delay(window)).ConfigureAwait(false) == task;
    }

    private static async Task ShouldComplete(Task task, string because)
    {
        try
        {
            await task.WaitAsync(Generous);
        }
        catch (TimeoutException)
        {
            Assert.Fail(because);
        }
    }

    private static async Task<T> ShouldComplete<T>(Task<T> task, string because)
    {
        try
        {
            return await task.WaitAsync(Generous);
        }
        catch (TimeoutException)
        {
            Assert.Fail(because);
            throw; // unreachable, Assert.Fail throws
        }
    }

    private static async Task ShouldBecomeTrue(Func<bool> condition, string because)
    {
        var deadline = Environment.TickCount64 + (long) Generous.TotalMilliseconds;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail(because);
            await Task.Delay(5);
        }
    }

    /// <summary>
    ///     Counts how many bodies are inside the limiter at once, and the highest that count ever reached.
    /// </summary>
    private sealed class ConcurrencyProbe
    {
        private int _current;
        private int _peak;

        public int Peak => Volatile.Read(ref _peak);

        public void Enter()
        {
            var running = Interlocked.Increment(ref _current);

            int seen;
            while (running > (seen = Volatile.Read(ref _peak)))
                Interlocked.CompareExchange(ref _peak, running, seen);
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _current);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Admission: the limit is a ceiling, and it is reachable.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    ///     The invariant the whole product leans on. Asserted over enough iterations that an over-admission
    ///     window of a single scheduling slice would be hit, and it holds however few cores are available:
    ///     a machine that never overlaps anything still cannot exceed the ceiling.
    /// </summary>
    [Fact]
    public async Task Begin_NeverAdmitsMoreThanMaxTasks()
    {
        const int limit = 4;
        const int iterations = 20_000;

        var resource = Limited(limit);
        var probe = new ConcurrencyProbe();

        var workers = Enumerable.Range(0, limit * 4).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < iterations / (limit * 4); i++)
            {
                using var job = await resource.Begin("work", 1, CancellationToken.None);
                probe.Enter();
                try
                {
                    await Task.Yield();
                }
                finally
                {
                    probe.Exit();
                }
            }
        })).ToArray();

        await ShouldComplete(Task.WhenAll(workers), "the limiter stopped handing out jobs");

        Assert.InRange(probe.Peak, 1, limit);
    }

    /// <summary>
    ///     The ceiling is worthless if the limiter admits one caller at a time in practice. Proving it can
    ///     reach the limit by watching for an overlap would be a coin flip on a constrained runner, so the
    ///     overlap is forced: every worker parks until all of them have been admitted. If the limiter would
    ///     only admit <c>limit - 1</c>, nobody is ever released and the generous timeout reports it.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Begin_ReachesMaxTasks(int limit)
    {
        var resource = Limited(limit);

        var arrived = 0;
        var allArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, limit).Select(_ => Task.Run(async () =>
        {
            using var job = await resource.Begin("rendezvous", 0, CancellationToken.None);
            if (Interlocked.Increment(ref arrived) == limit) allArrived.TrySetResult();
            await allArrived.Task;
        })).ToArray();

        try
        {
            await Task.WhenAll(workers).WaitAsync(Generous);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"only {Volatile.Read(ref arrived)} of {limit} jobs were admitted at once, so the " +
                        "limiter never reached MaxTasks");
        }
    }

    /// <summary>
    ///     A saturated limiter has to make the next caller wait rather than let it through, and it has to
    ///     let that caller through the moment a slot comes back.
    /// </summary>
    [Fact]
    public async Task Begin_WhenSaturated_QueuesRatherThanOverAdmitting()
    {
        var resource = Limited(1);

        var holder = await resource.Begin("holder", 0, CancellationToken.None);
        var queued = resource.Begin("queued", 0, CancellationToken.None).AsTask();

        await ShouldBecomeTrue(() => resource.StatusReport.Pending == 1,
            "the queued job was never counted as pending");

        Assert.False(await CompletedWithin(queued, Moment),
            "a second job was admitted while the only slot was still held");
        Assert.Equal(1, resource.StatusReport.Running);

        holder.Dispose();

        using var released = await ShouldComplete(queued, "releasing the only slot did not admit the queued job");
        Assert.True(released.Started);

        await ShouldBecomeTrue(() => resource.StatusReport is {Running: 1, Pending: 0},
            "the status report did not settle after the queued job started");
    }

    /// <summary>
    ///     A slot that is not returned shrinks the limit for the rest of the run and eventually stalls it,
    ///     so the release has to survive the body throwing.
    /// </summary>
    [Fact]
    public async Task Begin_WhenTheBodyThrows_StillReleasesTheSlot()
    {
        const int limit = 3;
        var resource = Limited(limit);

        for (var i = 0; i < limit * 10; i++)
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var job = await resource.Begin("throws", 0, CancellationToken.None);
                throw new InvalidOperationException("boom");
            });

        var jobs = new List<Job<TestResource>>();
        for (var i = 0; i < limit; i++)
            jobs.Add(await ShouldComplete(resource.Begin("after", 0, CancellationToken.None).AsTask(),
                $"only {i} of {limit} slots came back after the bodies threw"));

        foreach (var job in jobs) job.Dispose();
    }

    /// <summary>
    ///     Disposing twice is easy to do by accident (a <c>using</c> plus an explicit call). If the second
    ///     one released a slot the limiter would quietly run one wider for the rest of the process, which is
    ///     a worse failure than a leak because nothing ever stalls to reveal it.
    /// </summary>
    [Fact]
    public async Task Dispose_CalledTwice_DoesNotHandBackASlotTwice()
    {
        var resource = Limited(1);

        var job = await resource.Begin("first", 0, CancellationToken.None);
        job.Dispose();
        job.Dispose();

        using var holder = await ShouldComplete(resource.Begin("holder", 0, CancellationToken.None).AsTask(),
            "the slot was not returned at all");

        var queued = resource.Begin("queued", 0, CancellationToken.None).AsTask();
        Assert.False(await CompletedWithin(queued, Moment),
            "disposing twice released two slots, so the limiter now runs wider than MaxTasks");
    }

    /// <summary>
    ///     Cancelling while queued must give the slot up rather than consume one, otherwise a cancelled
    ///     install permanently narrows every resource it touched.
    /// </summary>
    [Fact]
    public async Task Begin_WhenCancelledWhileQueued_DoesNotConsumeASlot()
    {
        var resource = Limited(1);

        var holder = await resource.Begin("holder", 0, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var queued = resource.Begin("queued", 0, cts.Token).AsTask();
        await ShouldBecomeTrue(() => resource.StatusReport.Pending >= 1, "the queued job never queued");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Generous));

        holder.Dispose();

        using var next = await ShouldComplete(resource.Begin("next", 0, CancellationToken.None).AsTask(),
            "the cancelled job swallowed the slot it never started");
        Assert.True(next.Started);
    }

    /// <summary>
    ///     Documents current behaviour rather than endorsing it: a <see cref="Begin" /> cancelled while
    ///     queued leaves its job in the resource's job table forever, so <see cref="StatusReport.Pending" />
    ///     and <see cref="Resource{T}.Jobs" /> keep counting a job that will never run. Admission itself is
    ///     unaffected (see the test above) - this is a reporting leak, not a lost slot. If the leak is ever
    ///     fixed, this test is the one to delete.
    /// </summary>
    [Fact]
    public async Task Begin_WhenCancelledWhileQueued_LeavesTheJobCountedAsPending()
    {
        var resource = Limited(1);

        using var holder = await resource.Begin("holder", 0, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var queued = resource.Begin("queued", 0, cts.Token).AsTask();
        await ShouldBecomeTrue(() => resource.StatusReport.Pending >= 1, "the queued job never queued");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Generous));

        Assert.Equal(1, resource.StatusReport.Pending);
        Assert.Contains(resource.Jobs, j => j.Description == "queued");
    }

    /// <summary>
    ///     Callers read <see cref="Resource{T}.Jobs" /> to show what is in flight, so a finished job has to
    ///     leave it.
    /// </summary>
    [Fact]
    public async Task Jobs_ListsOnlyTheJobsThatHaveNotFinished()
    {
        var resource = Limited(4);

        var first = await resource.Begin("first", 10, CancellationToken.None);
        var second = await resource.Begin("second", 20, CancellationToken.None);

        Assert.Equal(new[] {"first", "second"}, resource.Jobs.Select(j => j.Description).OrderBy(d => d));
        Assert.Equal(2, resource.StatusReport.Running);

        first.Dispose();
        Assert.Equal(new[] {"second"}, resource.Jobs.Select(j => j.Description));

        second.Dispose();
        Assert.Empty(resource.Jobs);
        Assert.Equal(new StatusReport(0, 0, 0), resource.StatusReport);
    }

    /// <summary>
    ///     Documents current behaviour: <see cref="Resource{T}.MaxTasks" /> is settable but the semaphore is
    ///     sized once, in the constructor. Writing to it after construction changes what the property
    ///     reports and nothing else, so anything wanting a live thread-count setting needs a new resource.
    ///     <see cref="Resource{T}.MaxThroughput" /> is live (see the throughput tests) - the asymmetry is
    ///     easy to assume away.
    /// </summary>
    [Fact]
    public async Task MaxTasks_ChangedAfterConstruction_DoesNotChangeAdmission()
    {
        var resource = Limited(1);
        resource.MaxTasks = 4;

        using var holder = await resource.Begin("holder", 0, CancellationToken.None);
        var queued = resource.Begin("queued", 0, CancellationToken.None).AsTask();

        Assert.False(await CompletedWithin(queued, Moment),
            "raising MaxTasks after construction started resizing the limiter; update this test if that " +
            "is now intended");
    }

    // ---------------------------------------------------------------------------------------------
    // Accounting: every report lands exactly once.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    ///     Progress bars and throughput readouts are driven off this total, and it is written from every
    ///     reporting thread at once. Losing or doubling a report is a silent corruption, so the total is
    ///     asserted exactly against a count large enough to catch a lost increment.
    /// </summary>
    [Fact]
    public async Task Report_UnderParallelReporters_CountsEachReportExactlyOnce()
    {
        const int reporters = 8;
        const int perReporter = 250;
        const int size = 7;

        var resource = Limited(reporters);

        var workers = Enumerable.Range(0, reporters).Select(_ => Task.Run(async () =>
        {
            using var job = await resource.Begin("reporting", perReporter * size, CancellationToken.None);
            for (var i = 0; i < perReporter; i++)
                await resource.Report(job, size, CancellationToken.None);
        })).ToArray();

        await ShouldComplete(Task.WhenAll(workers), "a report never completed");

        Assert.Equal((long) reporters * perReporter * size, resource.StatusReport.Transferred);
    }

    /// <summary>
    ///     The no-wait path skips the channel entirely and adds straight to the total, which is the path the
    ///     hasher and the extractor take. Each reporter uses its own job because
    ///     <see cref="Resource{T}.ReportNoWait" /> updates <see cref="Job{T}.Current" /> with a plain
    ///     read-modify-write; the resource-wide total is the part that is interlocked.
    /// </summary>
    [Fact]
    public async Task ReportNoWait_UnderParallelReporters_CountsEachReportExactlyOnce()
    {
        const int reporters = 8;
        const int perReporter = 2_000;
        const int size = 3;

        var resource = Limited(reporters);

        var workers = Enumerable.Range(0, reporters).Select(_ => Task.Run(async () =>
        {
            using var job = await resource.Begin("reporting", perReporter * size, CancellationToken.None);
            for (var i = 0; i < perReporter; i++)
                resource.ReportNoWait(job, size);

            Assert.Equal((long) perReporter * size, job.Current);
        })).ToArray();

        await ShouldComplete(Task.WhenAll(workers), "a reporter never finished");

        Assert.Equal((long) reporters * perReporter * size, resource.StatusReport.Transferred);
    }

    /// <summary>
    ///     One caller giving up must not cost the others a byte of their accounting, and must not count a
    ///     byte of its own. A token that is already cancelled is refused before the report is ever queued.
    /// </summary>
    [Fact]
    public async Task Report_WhenOneCallerIsCancelled_LeavesEveryoneElsesAccountingIntact()
    {
        const int reporters = 8;
        const int perReporter = 250;
        const int size = 11;

        var resource = Limited(reporters + 1);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var workers = Enumerable.Range(0, reporters).Select(_ => Task.Run(async () =>
        {
            using var job = await resource.Begin("reporting", perReporter * size, CancellationToken.None);
            for (var i = 0; i < perReporter; i++)
                await resource.Report(job, size, CancellationToken.None);
        })).ToList();

        workers.Add(Task.Run(async () =>
        {
            using var job = await resource.Begin("cancelled", perReporter * size, CancellationToken.None);
            for (var i = 0; i < perReporter; i++)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await resource.Report(job, size, cancelled.Token));
        }));

        await ShouldComplete(Task.WhenAll(workers), "a report never completed");

        Assert.Equal((long) reporters * perReporter * size, resource.StatusReport.Transferred);
    }

    // ---------------------------------------------------------------------------------------------
    // Throughput: uncapped does not pace, capped does.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    ///     The default resource has no throughput cap and is on the hot path of every copy in the product,
    ///     so it must take the fast path through the pump rather than compute a delay. A cap of any size
    ///     applied to this volume would blow the timeout out by orders of magnitude, which is what makes the
    ///     one-sided assertion meaningful without measuring how long it actually took.
    /// </summary>
    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(0)] // Zero means "unlimited", not "nothing moves".
    public async Task Report_WhenThroughputIsUncapped_DoesNotPace(long maxThroughput)
    {
        const int reports = 2_000;
        const int size = 1024 * 1024;

        var resource = Throttled(1, maxThroughput);
        using var job = await resource.Begin("transferring", (long) reports * size, CancellationToken.None);

        await ShouldComplete(Task.Run(async () =>
        {
            for (var i = 0; i < reports; i++)
                await resource.Report(job, size, CancellationToken.None);
        }), $"{reports} uncapped reports did not get through, so the uncapped path is pacing");

        Assert.Equal((long) reports * size, resource.StatusReport.Transferred);
    }

    /// <summary>
    ///     The other half: a cap has to actually hold the caller. One byte per second against a hundred-byte
    ///     report is a hundred seconds of pacing, so "has it finished yet" answers the question without the
    ///     test knowing or caring how fast the machine is. The delay is not waited out - the assertion is
    ///     that it has not elapsed.
    /// </summary>
    [Fact]
    public async Task Report_WhenThroughputIsCapped_PacesTheCaller()
    {
        var resource = Throttled(1, 1);
        using var job = await resource.Begin("transferring", 100, CancellationToken.None);

        var report = resource.Report(job, 100, CancellationToken.None).AsTask();

        Assert.False(await CompletedWithin(report, Moment),
            "a hundred bytes reported against a one-byte-per-second cap came back immediately, so the cap " +
            "is not pacing anything");
    }

    /// <summary>
    ///     The pump reads <see cref="Resource{T}.MaxThroughput" /> per report, so the setting the user moves
    ///     in the UI takes effect on the run in progress. The first report proves the resource starts
    ///     uncapped; the second proves the new cap is honoured.
    /// </summary>
    [Fact]
    public async Task MaxThroughput_ChangedAfterConstruction_TakesEffectOnTheNextReport()
    {
        var resource = Throttled(1, long.MaxValue);
        using var job = await resource.Begin("transferring", 200, CancellationToken.None);

        await ShouldComplete(resource.Report(job, 100, CancellationToken.None).AsTask(),
            "an uncapped report did not complete");

        resource.MaxThroughput = 1;

        var capped = resource.Report(job, 100, CancellationToken.None).AsTask();
        Assert.False(await CompletedWithin(capped, Moment),
            "lowering MaxThroughput after construction had no effect on the next report");
    }

    /// <summary>
    ///     When a cap has the pump stalled, reports pile up behind a bounded channel. Cancelling has to
    ///     surface as a cancellation for the callers still waiting to be queued, rather than leaving them
    ///     parked. The pump's queue holds a handful of reports before it applies backpressure, so the bulk
    ///     of a large batch is guaranteed to be waiting on the writer; the exact split does not matter and
    ///     is not asserted.
    /// </summary>
    [Fact]
    public async Task Report_WhenQueuedBehindACap_SurfacesCancellationToTheWaitingCallers()
    {
        const int reports = 50;
        const int mustCancel = 30;

        var resource = Throttled(1, 1);
        using var job = await resource.Begin("transferring", reports * 1000, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var pending = Enumerable.Range(0, reports)
            .Select(_ => resource.Report(job, 1000, cts.Token).AsTask())
            .ToArray();

        cts.Cancel();

        await ShouldBecomeTrue(() => pending.Count(t => t.IsCompleted) >= mustCancel,
            $"fewer than {mustCancel} of {reports} reports queued behind a stalled cap noticed the " +
            "cancellation");

        foreach (var task in pending.Where(t => t.IsCompleted))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
}
