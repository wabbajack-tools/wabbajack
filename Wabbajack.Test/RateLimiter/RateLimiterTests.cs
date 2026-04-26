using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Tasks.Task;

namespace Wabbajack.RateLimiter.Test;

public class RateLimiter
{
    //[Fact]
    public async Task BasicTaskTests()
    {
        var rateLimiter = new Resource<int>("Test Resource", 2);

        var current = 0;
        var max = 0;
        object lockObj = new();

        void SetMax(object o, ref int i, ref int max1, int add)
        {
            lock (o)
            {
                i += add;
                max1 = Math.Max(i, max1);
            }
        }

        await Parallel.ForEachAsync(Enumerable.Range(0, 100),
            new ParallelOptions {MaxDegreeOfParallelism = 10},
            async (x, token) =>
            {
                using var job = await rateLimiter.Begin("Incrementing", 1, CancellationToken.None);
                SetMax(lockObj, ref current, ref max, 1);
                await Delay(10, token);
                SetMax(lockObj, ref current, ref max, -1);
            });

        Assert.Equal(2, max);
    }

    //[Fact]
    public async Task TestBasicThroughput()
    {
        var rateLimiter = new Resource<int>("Test Resource", 1, 1024 * 1024);

        using var job = await rateLimiter.Begin("Transferring", 1024 * 1024 * 5 / 2, CancellationToken.None);

        var sw = Stopwatch.StartNew();

        var report = rateLimiter.StatusReport;
        Assert.Equal(0, report.Transferred);
        foreach (var x in Enumerable.Range(0, 5)) await job.Report(1024 * 1024 / 2, CancellationToken.None);

        var elapsed = sw.Elapsed;
        Assert.True(elapsed > TimeSpan.FromSeconds(1));
        Assert.True(elapsed < TimeSpan.FromSeconds(3));

        report = rateLimiter.StatusReport;
        Assert.Equal(1024 * 1024 * 5 / 2, report.Transferred);
    }

    //[Fact]
    public async Task TestParallelThroughput()
    {
        var rateLimiter = new Resource<int>("Test Resource", 2, 1024 * 1024);


        var sw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (var i = 0; i < 4; i++)
            tasks.Add(Run(async () =>
            {
                using var job = await rateLimiter.Begin("Transferring", 1024 * 1024 / 10 * 5, CancellationToken.None);
                for (var x = 0; x < 5; x++) await job.Report(1024 * 1024 / 10, CancellationToken.None);
            }));

        await WhenAll(tasks.ToArray());
        var elapsed = sw.Elapsed;
        Assert.True(elapsed > TimeSpan.FromSeconds(1));
        Assert.True(elapsed < TimeSpan.FromSeconds(3));
    }

    //[Fact]
    public async Task TestParallelThroughputWithLimitedTasks()
    {
        var rateLimiter = new Resource<int>("Test Resource", 1, 1024 * 1024 * 4);
        ;

        var sw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (var i = 0; i < 4; i++)
            tasks.Add(Run(async () =>
            {
                using var job = await rateLimiter.Begin("Transferring", 1024 * 1024 / 10 * 5, CancellationToken.None);
                for (var x = 0; x < 5; x++) await job.Report(1024 * 1024 / 10, CancellationToken.None);
            }));

        await WhenAll(tasks.ToArray());
        var elapsed = sw.Elapsed;
        Assert.True(elapsed > TimeSpan.FromSeconds(0.5));
        Assert.True(elapsed < TimeSpan.FromSeconds(1.5));
    }
}