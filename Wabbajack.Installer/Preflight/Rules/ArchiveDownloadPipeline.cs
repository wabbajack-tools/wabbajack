using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     The installer's bulk download, reshaped for preflight: the logic of <c>AInstaller.DownloadArchives</c>,
///     <c>DownloadMissingArchives</c> and <c>DownloadArchive</c>, but nothing is silently dropped. Anything the
///     automated path cannot finish is handed to the user with the page to fetch it from, and the only thing
///     that stops the run is the user cancelling it.
///     <para>
///         Throughput is the dispatcher's business: every download runs under its own
///         <c>IResource&lt;DownloadDispatcher&gt;</c> job, which is also where byte progress is read from.
///     </para>
/// </summary>
public sealed class ArchiveDownloadPipeline
{
    /// <summary>Attempts at a download that stalls or is refused by the server before it goes manual.</summary>
    public const int TransientAttempts = 3;

    /// <summary>Attempts at a download whose bytes do not hash to what the list expects.</summary>
    public const int HashMismatchAttempts = 2;

    private const string DownloadingJobPrefix = "Downloading ";
    private static readonly TimeSpan ProgressTick = TimeSpan.FromMilliseconds(500);

    private readonly PreflightContext _ctx;
    private readonly IPreflightProgress _progress;
    private readonly ConcurrentDictionary<string, Archive> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly List<Archive> _downloaded = new();
    private readonly List<ManualQueueItem> _manual = new();
    private readonly List<(Archive Archive, string Reason)> _failed = new();

    public ArchiveDownloadPipeline(PreflightContext ctx, IPreflightProgress progress)
    {
        _ctx = ctx;
        _progress = progress;
    }

    /// <summary>The allow-list and mirror list, loaded together so one network failure is reported once.</summary>
    public sealed record DownloadPolicy(ServerAllowList AllowList, ILookup<Hash, Archive> Mirrors);

    /// <summary>
    ///     Where each missing archive goes. <see cref="Unsupported" /> is what neither a downloader nor a
    ///     browser can reach; unsupported-archives normally removes those before this runs.
    /// </summary>
    public sealed record Partition(List<Archive> Automated, List<ManualQueueItem> Manual, List<Archive> Unsupported);

    /// <summary>What the automated pass ended with, per archive.</summary>
    public sealed record Outcome(IReadOnlyList<Archive> Downloaded, IReadOnlyList<ManualQueueItem> Manual,
        IReadOnlyList<(Archive Archive, string Reason)> Failed);

    /// <summary>
    ///     What survives <see cref="Screen" />. <see cref="Ready" /> is what <see cref="Download" /> is given;
    ///     <see cref="Manual" /> is what the user has to fetch instead, and <see cref="Blocked" /> what
    ///     neither can reach.
    /// </summary>
    public sealed record Screening(List<Archive> Ready, IReadOnlyList<ManualQueueItem> Manual,
        IReadOnlyList<(Archive Archive, string Reason)> Blocked);

    /// <summary>Sources an automated downloader exists for, regardless of the account behind it.</summary>
    public static bool IsAutomatedType(IDownloadState state)
    {
        return state is WabbajackCDN or Http or Nexus;
    }

    /// <summary>
    ///     Sources this run downloads on its own. Nexus only counts with a premium account; a free account is
    ///     sent to the file page instead, one file at a time.
    /// </summary>
    public static bool IsAutomated(IDownloadState state, bool nexusPremium)
    {
        return state is WabbajackCDN or Http || (state is Nexus && nexusPremium);
    }

    /// <summary>
    ///     Splits by the state's type, never by which downloader the dispatcher would pick: while the old
    ///     browser-driven downloaders are still registered they would otherwise claim these archives.
    /// </summary>
    public static Partition Split(IEnumerable<Archive> missing, bool nexusPremium)
    {
        var automated = new List<Archive>();
        var manual = new List<ManualQueueItem>();
        var unsupported = new List<Archive>();

        foreach (var archive in missing)
        {
            if (IsAutomated(archive.State, nexusPremium))
                automated.Add(archive);
            else if (ManualDownloadUrls.TryGet(archive.State, out var target))
                manual.Add(new ManualQueueItem(archive, target, ManualReason(archive.State, target)));
            else
                unsupported.Add(archive);
        }

        return new Partition(automated, manual, unsupported);
    }

    /// <summary>
    ///     Points archives the mirror carries at the mirror, as the installer does. The archive objects are
    ///     the modlist's own, so the rerouted state is what the downloader is handed and what gets written to
    ///     the <c>.meta</c> file - which is the point, and also why the caller records what was rerouted on
    ///     <c>PreflightBlackboard.Rerouted</c>: after this, the list looks as though it always carried these
    ///     states.
    /// </summary>
    public static List<Archive> Reroute(IEnumerable<Archive> missing, ILookup<Hash, Archive> mirrors, ILogger logger)
    {
        var rerouted = new List<Archive>();
        foreach (var archive in missing)
        {
            var matches = mirrors[archive.Hash].ToArray();
            if (!matches.Any()) continue;

            archive.State = matches.First().State;
            logger.LogInformation("Rerouted {Archive} to {Mirror}", archive.Name,
                matches.First().State.PrimaryKeyString);
            rerouted.Add(archive);
        }

        return rerouted;
    }

    public async Task<DownloadPolicy> LoadPolicy(CancellationToken token)
    {
        _ctx.Logger.LogInformation("Downloading validation data");
        var allowList = await _ctx.DownloadPolicy.AllowList(token);
        var mirrors = await _ctx.DownloadPolicy.Mirrors(token);
        return new DownloadPolicy(allowList, mirrors);
    }

    /// <summary>
    ///     Whether Nexus archives in <paramref name="missing" /> download on their own. Both this and
    ///     nexus-login ask about the same set - what is still to be fetched - so the probe happens once, in
    ///     the check, whenever that set already held a Nexus archive. The gap left is the reroute below,
    ///     which can put a Nexus state on an archive that did not have one when the check looked; the account
    ///     is then probed here, once, and recorded on the blackboard for whatever asks next. Not being logged
    ///     in or premium is not a failure at this point, and deliberately does not stop the run the way
    ///     nexus-login would have: the archive goes to the manual queue with its Nexus page, as it would have
    ///     had the list carried it from the start.
    ///     <para>
    ///         A second pass over the same list has to reach the same conclusion, and that is what
    ///         <c>PreflightBlackboard.Rerouted</c> is for: the reroute has by then rewritten the archive's
    ///         state, so nexus-login would otherwise see a Nexus download the list never had and halt the run
    ///         over it - the one thing this paragraph says will not happen.
    ///     </para>
    /// </summary>
    public async Task<bool> NexusPremium(IEnumerable<Archive> missing, CancellationToken token)
    {
        if (_ctx.State.Nexus == null && missing.Any(a => a.State is Nexus))
        {
            _ctx.Logger.LogInformation("A mirror points at Nexus Mods, checking the account");
            try
            {
                _ctx.State.Nexus = await _ctx.NexusLogin.Probe(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _ctx.Logger.LogWarning(ex, "Could not check the Nexus Mods account, rerouted archives go to the manual queue");
            }
        }

        return _ctx.State.Nexus?.IsPremium == true;
    }

    /// <summary>
    ///     Fire-and-forget, as the installer's metrics always were; a failure to report is logged and
    ///     otherwise ignored.
    /// </summary>
    public void SendMetric(string action, string subject)
    {
        if (!_ctx.Options.SendMetrics) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _ctx.WjClient.SendMetric(action, subject);
            }
            catch (Exception ex)
            {
                _ctx.Logger.LogDebug(ex, "Metric {Action} was not sent", action);
            }
        });
    }

    /// <summary>
    ///     Which of <paramref name="automated" /> an automated download could actually start with: the
    ///     dispatcher has to have a downloader for it, that downloader has to prepare (an account with no
    ///     usable login fails here), and the allow-list has to permit the URL. None of those depends on
    ///     running a download, so they are answered while the split is being made rather than during the
    ///     download pass, and what they rule out reaches the manual queue in time for the check that walks
    ///     the user through it.
    /// </summary>
    public async Task<Screening> Screen(IReadOnlyList<Archive> automated, DownloadPolicy policy,
        CancellationToken token)
    {
        var ready = ApplyAllowList(await PrepareDownloaders(automated, token), policy.AllowList);
        lock (_sync)
        {
            return new Screening(ready, _manual.ToList(), _failed.ToList());
        }
    }

    /// <summary>
    ///     Downloads everything <see cref="Screen" /> passed. Only cancellation propagates; every other
    ///     problem lands in the outcome.
    /// </summary>
    public async Task<Outcome> Download(IReadOnlyList<Archive> ready, CancellationToken token)
    {
        if (_ctx.Options.SendMetrics)
        {
            foreach (var group in ready.GroupBy(a => a.State.GetType()))
                SendMetric($"downloading_{group.Key.FullName!.Split(".").Last().Split("+").First()}",
                    group.Sum(g => g.Size).ToString());
        }

        _ctx.Logger.LogInformation("Downloading {Count} archives", ready.Count);
        var total = ready.Count;
        var done = 0L;
        _progress.Report(0, total, $"Downloading 0 of {total}");

        using var tickCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var ticker = TickProgress(tickCts.Token);
        try
        {
            await ready
                .Shuffle()
                .PDoAll(async archive =>
                {
                    await DownloadOne(archive, token);
                    var finished = Interlocked.Increment(ref done);
                    _progress.Report(finished, total, $"Downloading {finished} of {total}");
                });
        }
        finally
        {
            tickCts.Cancel();
            await ticker;
        }

        lock (_sync)
        {
            return new Outcome(_downloaded.ToList(), _manual.ToList(), _failed.ToList());
        }
    }

    /// <summary>
    ///     <c>IDownloader.Prepare</c> takes no token of its own, so a login round-trip already in flight runs
    ///     to its end; cancelling stops the run before the next downloader is asked.
    /// </summary>
    private async Task<List<Archive>> PrepareDownloaders(IReadOnlyList<Archive> automated, CancellationToken token)
    {
        var byDownloader = new Dictionary<IDownloader, List<Archive>>();
        var ready = new List<Archive>();
        foreach (var archive in automated)
        {
            if (!_ctx.Dispatcher.TryGetDownloader(archive, out var downloader))
            {
                ToManual(archive, $"No downloader handles {archive.State.GetType().Name} sources");
                continue;
            }

            if (!byDownloader.TryGetValue(downloader, out var list))
                byDownloader[downloader] = list = new List<Archive>();
            list.Add(archive);
        }

        foreach (var (downloader, archives) in byDownloader)
        {
            token.ThrowIfCancellationRequested();
            bool prepared;
            try
            {
                prepared = await downloader.Prepare();
            }
            catch (Exception ex)
            {
                _ctx.Logger.LogWarning(ex, "{Downloader} could not be prepared", downloader.GetType().Name);
                prepared = false;
            }

            if (prepared)
            {
                ready.AddRange(archives);
                continue;
            }

            foreach (var archive in archives)
                ToManual(archive, $"Not logged in to {SiteName(archive.State)}");
        }

        return ready;
    }

    private List<Archive> ApplyAllowList(List<Archive> archives, ServerAllowList allowList)
    {
        var allowed = new List<Archive>(archives.Count);
        foreach (var archive in archives)
        {
            if (_ctx.Dispatcher.IsAllowed(archive, allowList))
            {
                allowed.Add(archive);
                continue;
            }

            // The installer used to stop the whole run here, without saying why. The user can still fetch
            // the file themselves, so it goes to the manual queue and the run carries on.
            _ctx.Logger.LogCritical("File {PrimaryKeyString} failed validation", archive.State.PrimaryKeyString);
            ToManual(archive, "Blocked by the download allow-list");
        }

        return allowed;
    }

    private async Task DownloadOne(Archive archive, CancellationToken token)
    {
        var destination = _ctx.Config.Downloads.Combine(archive.Name);
        _ctx.Logger.LogInformation("Downloading {Archive}", archive.Name);
        _progress.Archive(archive, ArchiveState.Downloading, null, 0);
        _active[archive.Name] = archive;

        try
        {
            // As the installer's DownloadMissingArchives did: when the dispatcher is set to proxy, the
            // archive is fetched (and its .meta written) through the proxy URL instead.
            archive = await _ctx.Dispatcher.MaybeProxy(archive, token);

            for (var attempt = 1; attempt <= HashMismatchAttempts; attempt++)
            {
                var (_, hash) = await WithRetries(() =>
                    _ctx.Dispatcher.DownloadWithPossibleUpgrade(archive, destination, token));

                if (hash != default && hash == archive.Hash)
                {
                    await Placed(archive, destination, hash, token);
                    return;
                }

                _ctx.Logger.LogError("Downloaded hash {Downloaded} for {Archive} does not match expected hash {Expected}",
                    hash, archive.Name, archive.Hash);
                if (destination.FileExists())
                    destination.Delete();
                if (attempt < HashMismatchAttempts)
                    _ctx.Logger.LogInformation("Downloading {Archive} again", archive.Name);
            }

            ToManual(archive, "The downloaded file did not match what the list expects");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (ManualDownloadRequiredException ex)
        {
            ToManual(archive, ex.Reason, ex.Target);
        }
        catch (DownloadTimeoutException ex)
        {
            _ctx.Logger.LogWarning(ex, "Gave up on {Archive} after {Attempts} stalled attempts", archive.Name,
                TransientAttempts);
            ToManual(archive, $"The download stalled {TransientAttempts} times");
        }
        catch (HttpException ex)
        {
            _ctx.Logger.LogWarning(ex, "Gave up on {Archive} after {Attempts} refused attempts", archive.Name,
                TransientAttempts);
            ToManual(archive, $"The server refused the download {TransientAttempts} times ({ex.Message})");
        }
        catch (Exception ex)
        {
            _ctx.Logger.LogError(ex, "Download error for file {Archive}", archive.Name);
            ToManual(archive, $"The download failed: {ex.Message}");
        }
        finally
        {
            _active.TryRemove(archive.Name, out _);
        }
    }

    private async Task Placed(Archive archive, AbsolutePath destination, Hash hash, CancellationToken token)
    {
        await _ctx.HashCache.FileHashWriteCache(destination, hash);
        await destination.WithExtension(Ext.Meta)
            .WriteAllTextAsync(_ctx.Dispatcher.MetaIniSection(archive), token);
        _ctx.State.HashedArchives[archive.Name] = destination;
        lock (_sync)
        {
            _downloaded.Add(archive);
        }

        _progress.Archive(archive, ArchiveState.Downloaded, null, archive.Size);
    }

    /// <summary>
    ///     A stalled transfer and a refusal from the server are each tried <see cref="TransientAttempts" />
    ///     times with a growing pause, the way the rest of the code base retries network calls.
    /// </summary>
    private async Task<(DownloadResult, Hash)> WithRetries(Func<Task<(DownloadResult, Hash)>> download)
    {
        var delay = _ctx.Options.DownloadRetryDelay;
        return await CircuitBreaker.WithAutoRetryAsync<(DownloadResult, Hash), DownloadTimeoutException>(_ctx.Logger,
            () => CircuitBreaker.WithAutoRetryAsync<(DownloadResult, Hash), HttpException>(_ctx.Logger, download,
                delay, maxRetries: TransientAttempts - 1).AsTask(),
            delay, maxRetries: TransientAttempts - 1);
    }

    private void ToManual(Archive archive, string reason, ManualDownloadTarget? target = null)
    {
        if (target == null && !ManualDownloadUrls.TryGet(archive.State, out target))
        {
            _ctx.Logger.LogError("{Archive} cannot be downloaded and has no page to send the user to: {Reason}",
                archive.Name, reason);
            lock (_sync)
            {
                _failed.Add((archive, reason));
            }

            _progress.Archive(archive, ArchiveState.Failed, reason);
            return;
        }

        _ctx.Logger.LogInformation("{Archive} moved to the manual queue: {Reason}", archive.Name, reason);
        lock (_sync)
        {
            _manual.Add(new ManualQueueItem(archive, target, reason));
        }

        _progress.Archive(archive, ArchiveState.ManualRequired, reason);
    }

    /// <summary>
    ///     Reads byte counts off the dispatcher's own jobs. The dispatcher names each job after the archive
    ///     it is fetching, which is the only handle this side has on a download in flight.
    /// </summary>
    private async Task TickProgress(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(ProgressTick, token);
                if (_active.IsEmpty) continue;

                foreach (var job in _ctx.DownloadLimiter.Jobs.ToList())
                {
                    if (job.Description == null || !job.Description.StartsWith(DownloadingJobPrefix, StringComparison.Ordinal))
                        continue;
                    var name = job.Description.Substring(DownloadingJobPrefix.Length);
                    if (_active.TryGetValue(name, out var archive))
                        _progress.Archive(archive, ArchiveState.Downloading, null, job.Current);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The downloads finished, or the run was cancelled.
        }
    }

    private static string ManualReason(IDownloadState state, ManualDownloadTarget target)
    {
        return state is Nexus
            ? "Nexus Mods only allows automated downloads for premium accounts"
            : $"{target.SiteName} downloads need a browser";
    }

    private static string SiteName(IDownloadState state)
    {
        return ManualDownloadUrls.TryGet(state, out var target) ? target.SiteName : state.GetType().Name;
    }
}
