using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Paths.IO;

/// <summary>How much patience a failed file operation has earned.</summary>
public enum IORetryKind
{
    /// <summary>Nothing about this failure will be different in a second. Throw it now.</summary>
    None,

    /// <summary>
    ///     An error nothing here recognises. A grace of about a second and a half, on the grounds that an
    ///     unknown IO failure is more often a moment of weather than a permanent state, but that guessing
    ///     should not cost what a diagnosed transient failure is allowed to cost.
    /// </summary>
    Brief,

    /// <summary>
    ///     A file someone else has open, or looks like one. These clear on their own and can take a while to
    ///     do it, so they get the full ten seconds.
    /// </summary>
    Full
}

/// <summary>Shape of the waiting: how long between attempts, how many, and a ceiling on the whole loop.</summary>
public readonly record struct IORetryPolicy
{
    /// <summary>Wait before the second attempt; each later wait doubles it up to <see cref="MaxDelay" />.</summary>
    public TimeSpan FirstDelay { get; init; }

    /// <summary>Ceiling for a single wait.</summary>
    public TimeSpan MaxDelay { get; init; }

    /// <summary>
    ///     Ceiling for the whole loop, attempts included. A cross-volume <c>File.Move</c> is a copy and a
    ///     delete rather than a rename, so an attempt can itself be minutes long; counting that against the
    ///     budget stops a doomed multi-gigabyte copy being run a dozen times.
    /// </summary>
    public TimeSpan Budget { get; init; }

    /// <summary>Attempts allowed for <see cref="IORetryKind.Full" />, the first one included.</summary>
    public int FullAttempts { get; init; }

    /// <summary>Attempts allowed for <see cref="IORetryKind.Brief" />, the first one included.</summary>
    public int BriefAttempts { get; init; }

    /// <summary>
    ///     Patience for a file another process holds open: fourteen attempts, waiting 50, 100, 200, 400 and
    ///     800ms and then a second at a time, which is about 9.6 seconds of waiting before the budget stops
    ///     it. That is the window the old eleven-attempt loop gave every failure alike, kept for the failures
    ///     that earn it, but reached through a backoff that starts at 50ms — so a hold lasting a moment now
    ///     costs a moment rather than up to a full second. An unrecognised failure gets six attempts over
    ///     about 1.55 seconds, and a diagnosed permanent one gets none.
    /// </summary>
    public static readonly IORetryPolicy Default = new()
    {
        FirstDelay = TimeSpan.FromMilliseconds(50),
        MaxDelay = TimeSpan.FromSeconds(1),
        Budget = TimeSpan.FromSeconds(10),
        FullAttempts = 14,
        BriefAttempts = 6
    };

    public int AttemptsFor(IORetryKind kind)
    {
        return kind switch
        {
            IORetryKind.Full => FullAttempts,
            IORetryKind.Brief => BriefAttempts,
            _ => 1
        };
    }
}

/// <summary>
///     Retrying a file operation that failed, for the failures where that can work.
///     <para>
///         A file briefly held open by a virus scanner or another process is the case worth waiting for, and
///         on Windows it is common enough that the file helpers here have always retried. A destination that
///         is a directory, a path the volume cannot hold, a full disk or a missing source will fail exactly
///         the same way on the eleventh attempt as on the first, so waiting only delays the real error behind
///         a row of pointless sleeps.
///     </para>
///     <para>
///         The error code alone does not separate every case, so a caller that knows something about its own
///         operation should wrap <see cref="Classify" /> and downgrade the answer — Windows reports a
///         destination that is an existing directory as ERROR_ACCESS_DENIED, the same code it reports for a
///         destination another process has open, and those two deserve opposite treatment.
///         <see cref="ClassifyMove" /> is that wrapping for <c>File.Move</c>, and the worked example.
///     </para>
/// </summary>
public static class IORetry
{
    /// <summary>
    ///     What a failure is worth on its error code alone.
    ///     <list type="bullet">
    ///         <item>A sharing or lock violation is the unambiguous "come back later": full patience.</item>
    ///         <item>
    ///             So is access denied, which is the same thing wearing a different code. Windows reports a
    ///             destination another process has open as ERROR_ACCESS_DENIED whatever sharing that process
    ///             allowed — a reader holding it with <c>FileShare.Read</c> reads the same as an exclusive
    ///             writer — and reports a delete that is still pending the same way. A virus scanner, Mod
    ///             Organizer or Explorer holding the destination is the commonest recoverable failure there
    ///             is on Windows, so it gets the whole window. The cost is that a real permission problem
    ///             waits it out too, which is what this helper has always done.
    ///         </item>
    ///         <item>
    ///             A missing file or directory, a full disk, a path the volume cannot hold and a destination
    ///             that already exists where replacing it was not allowed are all permanent. So is anything
    ///             that is not an IO exception at all — an <see cref="ArgumentException" /> for a malformed
    ///             path is a caller bug, not a busy disk.
    ///         </item>
    ///         <item>An IO error with none of those codes is unknown rather than permanent: a brief grace.</item>
    ///     </list>
    /// </summary>
    public static IORetryKind Classify(Exception ex)
    {
        switch (ex)
        {
            // Never retried and never swallowed; RunAsync rethrows these before it asks.
            case OperationCanceledException:
            case FileNotFoundException:
            case DirectoryNotFoundException:
                return IORetryKind.None;

            case UnauthorizedAccessException:
                return IORetryKind.Full;

            case IOException io:
                if (IOErrors.IsSharingViolation(io)) return IORetryKind.Full;
                if (IOErrors.IsDiskFull(io) || IOErrors.IsBadPath(io) || IOErrors.IsAlreadyExists(io))
                    return IORetryKind.None;
                return IORetryKind.Brief;

            default:
                return IORetryKind.None;
        }
    }

    /// <summary>
    ///     What a failed <c>File.Move</c> is worth, its error code and its circumstances together. Two things
    ///     the code cannot tell you are settled by looking instead:
    ///     <list type="bullet">
    ///         <item>
    ///             A destination that is an existing directory. Windows calls that access denied, which is
    ///             also what a destination another process has open is called, and Unix calls it a plain
    ///             <see cref="IOException" />. A directory is not going to stop being one, so it is permanent
    ///             whichever code arrived.
    ///         </item>
    ///         <item>
    ///             A source that is gone. Retrying cannot bring it back — but only a file absent from a
    ///             folder that can still be read counts, see <see cref="LooksGone" />.
    ///         </item>
    ///     </list>
    /// </summary>
    public static IORetryKind ClassifyMove(Exception ex, string source, string destination)
    {
        var kind = Classify(ex);
        if (kind == IORetryKind.None) return kind;
        if (Directory.Exists(destination)) return IORetryKind.None;
        if (LooksGone(source)) return IORetryKind.None;
        return kind;
    }

    /// <summary>
    ///     Whether a path can be said to be absent, as opposed to merely unreadable right now.
    ///     <para>
    ///         <see cref="File.Exists(string)" /> answers false for every failure, not only for absence: a
    ///         network share that blinked, or a filter driver refusing the parent folder, both read as "not
    ///         there" from a file that is perfectly present. Calling that permanent would be the exact mistake
    ///         this class exists to avoid, and the acquirer watches a folder that can be a network location.
    ///         So a file only counts as gone when the folder holding it can still be read.
    ///     </para>
    /// </summary>
    public static bool LooksGone(string path)
    {
        if (File.Exists(path)) return false;
        var parent = Path.GetDirectoryName(path);
        return !string.IsNullOrEmpty(parent) && Directory.Exists(parent);
    }

    /// <summary>Runs <paramref name="attempt" /> until it succeeds or its failure is not worth repeating.</summary>
    public static ValueTask RunAsync(Action attempt, CancellationToken token)
    {
        return RunAsync(attempt, Classify, IORetryPolicy.Default, token);
    }

    /// <summary>
    ///     Runs <paramref name="attempt" />, waiting and trying again for as long as
    ///     <paramref name="classify" /> says the failure deserves. The exception the caller sees is always the
    ///     one the attempt threw, rethrown rather than wrapped; cancellation comes out immediately, whether it
    ///     arrives during an attempt or during a wait.
    /// </summary>
    public static async ValueTask RunAsync(Action attempt, Func<Exception, IORetryKind> classify,
        IORetryPolicy policy, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var delay = policy.FirstDelay;
        var attempts = 0;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            attempts++;

            try
            {
                attempt();
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var kind = classify(ex);
                if (kind == IORetryKind.None || attempts >= policy.AttemptsFor(kind) ||
                    clock.Elapsed + delay > policy.Budget)
                    throw;

                await Task.Delay(delay, token).ConfigureAwait(false);
                delay = delay >= policy.MaxDelay ? policy.MaxDelay : delay + delay;
                if (delay > policy.MaxDelay) delay = policy.MaxDelay;
            }
        }
    }
}
