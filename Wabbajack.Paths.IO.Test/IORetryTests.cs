using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

/// <summary>
///     The classification is a pure function of the exception, and the loop is driven here by a fake attempt
///     rather than by the file system, so every case is deterministic. Nothing asserts how long anything took:
///     the tests count attempts.
/// </summary>
public class IORetryTests
{
    /// <summary>Small enough that the whole class runs in a few tens of milliseconds.</summary>
    private static readonly IORetryPolicy Fast = new()
    {
        FirstDelay = TimeSpan.FromMilliseconds(1),
        MaxDelay = TimeSpan.FromMilliseconds(4),
        Budget = TimeSpan.FromSeconds(30),
        FullAttempts = 6,
        BriefAttempts = 3
    };

    public static IEnumerable<object[]> Classifications()
    {
        // Worth waiting for: someone else has the file open.
        yield return new object[] {new IOException("held", IOErrors.SharingViolation), IORetryKind.Full};
        yield return new object[] {new IOException("locked", IOErrors.LockViolation), IORetryKind.Full};
        yield return new object[]
            {new IOException("The process cannot access the file because it is being used by another process."), IORetryKind.Full};

        // Ambiguous: Windows says access denied for a held destination and for a permission problem alike.
        yield return new object[] {new UnauthorizedAccessException("denied"), IORetryKind.Brief};
        yield return new object[] {new IOException("something else went wrong"), IORetryKind.Brief};

        // Nothing another attempt can change.
        yield return new object[] {new FileNotFoundException("gone"), IORetryKind.None};
        yield return new object[] {new DirectoryNotFoundException("no folder"), IORetryKind.None};
        yield return new object[] {new PathTooLongException("too long"), IORetryKind.None};
        yield return new object[] {new IOException("bad name", IOErrors.InvalidName), IORetryKind.None};
        yield return new object[] {new IOException("full", IOErrors.DiskFull), IORetryKind.None};
        yield return new object[] {new IOException("full", IOErrors.HandleDiskFull), IORetryKind.None};
        yield return new object[] {new IOException("exists", IOErrors.AlreadyExists), IORetryKind.None};
        yield return new object[] {new IOException("exists", IOErrors.FileExists), IORetryKind.None};
        yield return new object[] {new ArgumentException("not a path"), IORetryKind.None};
        yield return new object[] {new NotSupportedException("no"), IORetryKind.None};
        yield return new object[] {new OperationCanceledException(), IORetryKind.None};
    }

    [Theory]
    [MemberData(nameof(Classifications))]
    public void ClassifiesFailuresByWhetherAnotherAttemptCouldWork(Exception ex, IORetryKind expected)
    {
        Assert.Equal(expected, IORetry.Classify(ex));
    }

    [Fact]
    public async Task ATransientFailureIsRetriedUntilItSucceeds()
    {
        var attempts = 0;
        await IORetry.RunAsync(() =>
        {
            attempts++;
            if (attempts < 3) throw new IOException("held", IOErrors.SharingViolation);
        }, IORetry.Classify, Fast, CancellationToken.None);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task APermanentFailureIsThrownFromTheFirstAttempt()
    {
        var attempts = 0;
        var thrown = new IOException("full", IOErrors.DiskFull);

        var caught = await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw thrown;
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Same(thrown, caught);
    }

    [Fact]
    public async Task AnAmbiguousFailureStopsAtTheShortLimit()
    {
        var attempts = 0;
        var thrown = new UnauthorizedAccessException("denied");

        var caught = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw thrown;
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(Fast.BriefAttempts, attempts);
        Assert.Same(thrown, caught);
    }

    [Fact]
    public async Task ATransientFailureThatNeverClearsStopsAtTheFullLimit()
    {
        var attempts = 0;
        var thrown = new IOException("held", IOErrors.SharingViolation);

        var caught = await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw thrown;
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(Fast.FullAttempts, attempts);
        Assert.Same(thrown, caught);
    }

    /// <summary>A caller can say more about its own operation than the error code does, and be believed.</summary>
    [Fact]
    public async Task ACallerCanDowngradeAFailureTheErrorCodeCallsTransient()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw new IOException("held", IOErrors.SharingViolation);
            }, _ => IORetryKind.None, Fast, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task TheBudgetBoundsTheWholeLoop()
    {
        var attempts = 0;
        var spent = Fast with {Budget = TimeSpan.Zero};

        await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw new IOException("held", IOErrors.SharingViolation);
            }, IORetry.Classify, spent, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ACancelledTokenIsSeenBeforeTheFirstAttempt()
    {
        var attempts = 0;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await IORetry.RunAsync(() => attempts++, IORetry.Classify, Fast, cts.Token));

        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task CancellationDuringAWaitEndsTheLoopRatherThanRetrying()
    {
        var attempts = 0;
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                cts.Cancel();
                throw new IOException("held", IOErrors.SharingViolation);
            }, IORetry.Classify, Fast, cts.Token));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task AnAttemptThatCancelsIsNeverRetriedOrSwallowed()
    {
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw new OperationCanceledException();
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public void TheDefaultPolicyGivesPatienceOnlyWhereItIsEarned()
    {
        Assert.Equal(1, IORetryPolicy.Default.AttemptsFor(IORetryKind.None));
        Assert.True(IORetryPolicy.Default.AttemptsFor(IORetryKind.Brief) <
                    IORetryPolicy.Default.AttemptsFor(IORetryKind.Full));
        Assert.True(IORetryPolicy.Default.FirstDelay < IORetryPolicy.Default.MaxDelay);
    }
}
