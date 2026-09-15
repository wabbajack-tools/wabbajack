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
public class IORetryTests : IDisposable
{
    /// <summary>Real folders, for the cases that classify by looking at the disk rather than at a code.</summary>
    private readonly AbsolutePath _root = KnownFolders.EntryPoint.Combine("ioretry-" + Guid.NewGuid());

    public IORetryTests()
    {
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        _root.DeleteDirectory();
    }

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

        // Access denied is the other code Windows uses for a destination another process has open, whatever
        // sharing that process allowed, so it is worth the same window as a sharing violation.
        yield return new object[] {new UnauthorizedAccessException("denied"), IORetryKind.Full};

        // An IO error nothing here recognises: unknown rather than permanent.
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
    public async Task AnUnrecognisedFailureStopsAtTheShortLimit()
    {
        var attempts = 0;
        var thrown = new IOException("something else went wrong");

        var caught = await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw thrown;
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(Fast.BriefAttempts, attempts);
        Assert.Same(thrown, caught);
    }

    /// <summary>
    ///     A destination another process holds open reports access denied rather than a sharing violation,
    ///     and it is the commonest recoverable failure there is, so it must get the long window and not the
    ///     short one.
    /// </summary>
    [Fact]
    public async Task ADeniedDestinationIsGivenTheFullWindow()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw new UnauthorizedAccessException("denied");
            }, IORetry.Classify, Fast, CancellationToken.None));

        Assert.Equal(Fast.FullAttempts, attempts);
        Assert.True(Fast.FullAttempts > Fast.BriefAttempts);
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

    /// <summary>
    ///     The budget covers the attempts and not only the waiting between them, which is what stops a doomed
    ///     cross-volume copy — a whole-file copy and delete rather than a rename — being run a dozen times
    ///     over. The attempt here costs more than the entire budget while the waits cost a millisecond, so a
    ///     loop that timed only its own sleeps would run to the attempt limit instead of stopping at one.
    /// </summary>
    [Fact]
    public async Task ASlowAttemptSpendsTheBudgetItself()
    {
        var attempts = 0;
        var tight = Fast with {Budget = TimeSpan.FromMilliseconds(50)};

        await Assert.ThrowsAsync<IOException>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                Thread.Sleep(200);
                throw new IOException("held", IOErrors.SharingViolation);
            }, IORetry.Classify, tight, CancellationToken.None));

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

    /// <summary>
    ///     Counts the attempts a move classifier allows for a failure the error code alone calls transient.
    ///     Without the circumstances, every one of these would run to <see cref="IORetryPolicy.FullAttempts" />.
    /// </summary>
    private async Task<int> AttemptsForMove(string source, string destination)
    {
        var attempts = 0;
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IORetry.RunAsync(() =>
            {
                attempts++;
                throw new UnauthorizedAccessException("denied");
            }, ex => IORetry.ClassifyMove(ex, source, destination), Fast, CancellationToken.None));
        return attempts;
    }

    /// <summary>
    ///     The line that makes the squatting-directory case fail at once on both platforms. Delete the
    ///     destination check from ClassifyMove and this test counts a full window of attempts instead of one.
    /// </summary>
    [Fact]
    public async Task ADestinationThatIsADirectoryIsNotRetriedAtAll()
    {
        var source = _root.Combine("src.bin");
        source.WriteAllText("data");
        var destination = _root.Combine("dest.bin");
        destination.CreateDirectory();

        Assert.Equal(IORetryKind.None,
            IORetry.ClassifyMove(new UnauthorizedAccessException("denied"), source.ToString(), destination.ToString()));
        Assert.Equal(1, await AttemptsForMove(source.ToString(), destination.ToString()));
    }

    [Fact]
    public async Task ASourceMissingFromAFolderThatCanBeReadIsNotRetriedAtAll()
    {
        var source = _root.Combine("never-existed.bin");
        var destination = _root.Combine("dest.bin");

        Assert.True(IORetry.LooksGone(source.ToString()));
        Assert.Equal(1, await AttemptsForMove(source.ToString(), destination.ToString()));
    }

    /// <summary>
    ///     File.Exists answers false for a folder it cannot read as readily as for a file that is not there,
    ///     and the acquirer's watch folder can be a network location. A source that cannot be seen is not the
    ///     same as one that is gone, and must not be given the permanent verdict.
    /// </summary>
    [Fact]
    public async Task ASourceWhoseFolderCannotBeReadIsNotCalledGone()
    {
        var unreachable = _root.Combine("vanished-share", "src.bin");
        var destination = _root.Combine("dest.bin");

        Assert.False(IORetry.LooksGone(unreachable.ToString()));
        Assert.Equal(Fast.FullAttempts, await AttemptsForMove(unreachable.ToString(), destination.ToString()));
    }

    [Fact]
    public void AMoveKeepsThePermanentVerdictsOfTheCodeItself()
    {
        var source = _root.Combine("src.bin");
        source.WriteAllText("data");
        var destination = _root.Combine("dest.bin");

        Assert.Equal(IORetryKind.None,
            IORetry.ClassifyMove(new IOException("full", IOErrors.DiskFull), source.ToString(), destination.ToString()));
        Assert.Equal(IORetryKind.Full,
            IORetry.ClassifyMove(new IOException("held", IOErrors.SharingViolation), source.ToString(),
                destination.ToString()));
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
