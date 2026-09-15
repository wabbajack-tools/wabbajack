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
    ///     Might be a handle that is about to close, might be permanent; Windows reports both the same way.
    ///     Worth a short grace, not worth a long one.
    /// </summary>
    Brief,

    /// <summary>A file someone else has open. These clear on their own, and can take a while to do it.</summary>
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
    ///     Ten seconds of patience for a file another process holds open — the same window the old
    ///     eleven-attempt loop gave it — but reached through a backoff that starts at 50ms, so a hold that
    ///     lasts a moment costs a moment rather than a full second. An ambiguous failure gets five attempts
    ///     over about a second and a half, and a permanent one gets none.
    /// </summary>
    public static readonly IORetryPolicy Default = new()
    {
        FirstDelay = TimeSpan.FromMilliseconds(50),
        MaxDelay = TimeSpan.FromSeconds(1),
        Budget = TimeSpan.FromSeconds(10),
        FullAttempts = 14,
        BriefAttempts = 5
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
///         Callers that know something about their operation should wrap <see cref="Classify" /> and downgrade
///         its answer, because the error codes alone do not separate every case: Windows reports a destination
///         that is an existing directory as ERROR_ACCESS_DENIED, which is the same code it reports for a
///         destination another process has open.
///     </para>
/// </summary>
public static class IORetry
{
    /// <summary>
    ///     What a failure is worth on its error code alone.
    ///     <list type="bullet">
    ///         <item>A sharing or lock violation is the one unambiguous "come back later": full patience.</item>
    ///         <item>
    ///             A missing file or directory, a full disk, a path the volume cannot hold and a destination
    ///             that already exists where replacing it was not allowed are all permanent. So is anything
    ///             that is not an IO exception at all — an <see cref="ArgumentException" /> for a malformed
    ///             path is a caller bug, not a busy disk.
    ///         </item>
    ///         <item>
    ///             Access denied is deliberately not permanent. Windows returns it for a destination another
    ///             process holds open and for a file whose delete is still pending, as well as for a genuine
    ///             permission problem, so it gets the short grace: a real permission failure surfaces in about
    ///             a second and a half instead of eleven seconds, and a handle closing in the meantime still
    ///             recovers.
    ///         </item>
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
                return IORetryKind.Brief;

            case IOException io:
                if (IOErrors.IsSharingViolation(io)) return IORetryKind.Full;
                if (IOErrors.IsDiskFull(io) || IOErrors.IsBadPath(io) || IOErrors.IsAlreadyExists(io))
                    return IORetryKind.None;
                return IORetryKind.Brief;

            default:
                return IORetryKind.None;
        }
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
