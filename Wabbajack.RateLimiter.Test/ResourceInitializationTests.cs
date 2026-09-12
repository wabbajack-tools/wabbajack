using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.RateLimiter.Test;

public class ResourceInitializationTests
{
    private sealed class TestResource
    {
    }

    /// <summary>
    ///     The settings constructor reads its limits asynchronously, so the semaphore does not exist yet when
    ///     the constructor returns. Every resource in the app is built this way and callers can reach Begin
    ///     immediately, so Begin has to wait for that rather than dereference a null.
    /// </summary>
    [Fact]
    public async Task Begin_BeforeSettingsHaveLoaded_WaitsRatherThanThrowing()
    {
        var resource = new Resource<TestResource>("Slow settings",
            async () =>
            {
                await Task.Delay(250);
                return (2, long.MaxValue);
            });

        using var job = await resource.Begin("first", 0, CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(job);
        Assert.True(job.Started);
        Assert.Equal(2, resource.MaxTasks);
    }

    [Fact]
    public async Task Begin_BeforeSettingsHaveLoaded_StillHonoursTheLimit()
    {
        var resource = new Resource<TestResource>("Slow settings",
            async () =>
            {
                await Task.Delay(250);
                return (1, long.MaxValue);
            });

        var first = await resource.Begin("first", 0, CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));

        var second = resource.Begin("second", 0, CancellationToken.None).AsTask();
        var finishedFirst = await Task.WhenAny(second, Task.Delay(250)) != second;
        Assert.True(finishedFirst, "the second job started while the first still held the only slot");

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(10))).Dispose();
    }

    /// <summary>
    ///     A failing settings read should surface as itself, rather than as a null reference somewhere later.
    /// </summary>
    [Fact]
    public async Task Begin_WhenSettingsFail_SurfacesTheOriginalError()
    {
        var resource = new Resource<TestResource>("Broken settings",
            () => throw new InvalidOperationException("settings unavailable"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resource.Begin("first", 0, CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal("settings unavailable", ex.Message);
    }
}
