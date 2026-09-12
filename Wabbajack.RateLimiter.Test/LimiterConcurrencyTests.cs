using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.RateLimiter.Test;

/// <summary>
///     The parallel extensions in <see cref="AsyncParallelExtensions" /> are what keep the compiler,
///     installer and extractor from starting more work than the user's settings allow. An overload that
///     takes a limiter and then ignores it looks correct at the call site and silently runs unbounded, so
///     each one is measured here against a low limit.
/// </summary>
public class LimiterConcurrencyTests
{
    private const int Items = 200;
    private const int Limit = 4;

    private sealed class TestResource
    {
    }

    /// <summary>
    ///     Records the highest number of bodies running at the same time.
    /// </summary>
    private sealed class ConcurrencyProbe
    {
        private int _current;
        private int _peak;

        public int Peak => Volatile.Read(ref _peak);

        public async Task<T> Run<T>(T value)
        {
            var running = Interlocked.Increment(ref _current);

            int seen;
            while (running > (seen = Volatile.Read(ref _peak)))
                Interlocked.CompareExchange(ref _peak, running, seen);

            try
            {
                // Long enough that overlapping callers actually overlap.
                await Task.Delay(5);
                return value;
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }
    }

    private static Resource<TestResource> NewLimiter()
    {
        return new Resource<TestResource>("Test", Limit);
    }

    [Fact]
    public async Task PMapAll_WithLimiter_BoundsConcurrency()
    {
        var probe = new ConcurrencyProbe();
        var limiter = NewLimiter();

        var results = await Enumerable.Range(0, Items)
            .PMapAll(limiter, probe.Run)
            .ToList();

        Assert.Equal(Items, results.Count);
        Assert.InRange(probe.Peak, 1, Limit);
    }

    /// <summary>
    ///     Callers index into the result of PMapAll expecting it to line up with the input, so limiting must
    ///     not reorder anything.
    /// </summary>
    [Fact]
    public async Task PMapAll_WithLimiter_PreservesInputOrder()
    {
        var limiter = NewLimiter();

        var results = await Enumerable.Range(0, Items)
            .PMapAll(limiter, async i =>
            {
                // Reverse the natural completion order, so anything yielding on completion fails here.
                await Task.Delay((Items - i) % 20);
                return i;
            })
            .ToList();

        Assert.Equal(Enumerable.Range(0, Items).ToList(), results);
    }

    [Fact]
    public async Task PKeepAll_WithLimiter_BoundsConcurrencyAndDropsNulls()
    {
        var probe = new ConcurrencyProbe();
        var limiter = NewLimiter();

        var results = await Enumerable.Range(0, Items)
            .PKeepAll(limiter, async i => await probe.Run(i % 2 == 0 ? i.ToString() : null!))
            .ToList();

        Assert.Equal(Items / 2, results.Count);
        Assert.All(results, r => Assert.NotNull(r));
        Assert.InRange(probe.Peak, 1, Limit);
    }

    [Fact]
    public async Task PDoAll_WithLimiter_BoundsConcurrency()
    {
        var probe = new ConcurrencyProbe();
        var limiter = NewLimiter();

        var seen = new List<int>();
        await Enumerable.Range(0, Items)
            .PDoAll(limiter, async i =>
            {
                await probe.Run(i);
                lock (seen) seen.Add(i);
            });

        Assert.Equal(Items, seen.Count);
        Assert.InRange(probe.Peak, 1, Limit);
    }

    [Fact]
    public async Task PMapAllBatchedAsync_BoundsConcurrency()
    {
        var probe = new ConcurrencyProbe();
        var limiter = NewLimiter();

        var results = await Enumerable.Range(0, Items)
            .PMapAllBatchedAsync(limiter, probe.Run)
            .ToList();

        Assert.Equal(Items, results.Count);
        Assert.InRange(probe.Peak, 1, Limit);
    }

    [Fact]
    public async Task PDoAllBatched_BoundsConcurrency()
    {
        var probe = new ConcurrencyProbe();
        var limiter = NewLimiter();

        var count = 0;
        await Enumerable.Range(0, Items)
            .PDoAllBatched<int, TestResource, int>(limiter, async i =>
            {
                await probe.Run(i);
                Interlocked.Increment(ref count);
            });

        Assert.Equal(Items, count);
        Assert.InRange(probe.Peak, 1, Limit);
    }

    /// <summary>
    ///     Every job must be returned to the limiter, including when the body throws. A leaked permit shrinks
    ///     the limit for the rest of the run and eventually stalls it.
    /// </summary>
    [Fact]
    public async Task PMapAll_WithLimiter_ReleasesJobsWhenTheBodyThrows()
    {
        var limiter = NewLimiter();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await Enumerable.Range(0, Items)
                .PMapAll(limiter, async i =>
                {
                    await Task.Yield();
                    if (i % 10 == 0) throw new InvalidOperationException("boom");
                    return i;
                })
                .ToList());

        // If any permit leaked, acquiring Limit jobs here would block.
        var jobs = new List<Job<TestResource>>();
        for (var i = 0; i < Limit; i++)
            jobs.Add(await limiter.Begin("after", 0, CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10)));

        foreach (var job in jobs) job.Dispose();
    }
}
