#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     Real folders, real files, real hashing, fast timings. Anything that depends on an exclusive open
///     failing while another handle is held is Windows-only: <c>FileShare.None</c> is advisory on Linux.
/// </summary>
public class ManualDownloadAcquirerTests : IAsyncDisposable
{
    private readonly PreflightTestFolder _folder = new();
    private readonly List<ManualDownloadAcquirer> _acquirers = new();
    private readonly ILogger<ManualDownloadAcquirer> _logger;
    private readonly FileHashCache _hashCache;
    private readonly IResource<FileHashCache> _hashLimiter;
    private readonly DownloadDispatcher _dispatcher;

    public ManualDownloadAcquirerTests(ILogger<ManualDownloadAcquirer> logger, FileHashCache hashCache,
        IResource<FileHashCache> hashLimiter, DownloadDispatcher dispatcher)
    {
        _logger = logger;
        _hashCache = hashCache;
        _hashLimiter = hashLimiter;
        _dispatcher = dispatcher;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var acquirer in _acquirers)
            await acquirer.DisposeAsync();
        _folder.Dispose();
    }

    private static ManualDownloadAcquirerOptions FastOptions(bool watcher = true, bool forceCopy = false,
        TimeSpan? poll = null)
    {
        return new ManualDownloadAcquirerOptions
        {
            PollInterval = poll ?? TimeSpan.FromMilliseconds(100),
            StableInterval = TimeSpan.FromMilliseconds(50),
            StableSamples = 2,
            LockRetryDelay = TimeSpan.FromMilliseconds(50),
            LockRetryCap = TimeSpan.FromMilliseconds(200),
            UseFileSystemWatcher = watcher,
            ForceCopyPlacement = forceCopy
        };
    }

    private ManualDownloadAcquirer NewAcquirer(ManualDownloadAcquirerOptions? options = null,
        IResource<FileHashCache>? limiter = null)
    {
        var acquirer = new ManualDownloadAcquirer(_logger, _hashCache, limiter ?? _hashLimiter, _dispatcher,
            options ?? FastOptions());
        _acquirers.Add(acquirer);
        return acquirer;
    }

    private async Task<ManualDownloadAcquirer> Started(IEnumerable<Archive> archives,
        ManualDownloadAcquirerOptions? options = null, AbsolutePath? watch = null, AbsolutePath? destination = null,
        CancellationToken token = default)
    {
        var acquirer = NewAcquirer(options);
        await acquirer.Start(archives, watch ?? _folder.Watch, destination ?? _folder.Destination, token);
        return acquirer;
    }

    private static ManualDownloadItem Item(IManualDownloadAcquirer acquirer, string key)
    {
        return acquirer.Snapshot().Single(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static Task WaitForState(IManualDownloadAcquirer acquirer, string key, ManualDownloadState state)
    {
        return PreflightTestFolder.WaitUntil(() => Item(acquirer, key).State == state,
            $"{key} to reach {state} (now {Item(acquirer, key).State}: {Item(acquirer, key).Message})");
    }

    private async Task AssertPlaced(ManualDownloadAcquirer acquirer, Archive archive, byte[] bytes)
    {
        var dest = _folder.Destination.Combine(archive.Name);
        Assert.True(dest.FileExists(), $"{dest} should exist");
        Assert.Equal(bytes, await dest.ReadAllBytesAsync());

        var item = Item(acquirer, archive.Name);
        Assert.Equal(ManualDownloadState.Moved, item.State);
        Assert.Equal(dest, item.PlacedPath);

        var meta = dest.WithExtension(Ext.Meta);
        Assert.True(meta.FileExists(), $"{meta} should exist");
        var lines = meta.ReadAllLines().ToList();
        Assert.Equal("[General]", lines[0]);
        Assert.Equal("installed=true", lines[1]);
        Assert.Contains(lines, l => l.StartsWith("manualURL="));

        Assert.Equal(archive.Hash, await _hashCache.TryGetHashCache(dest));
    }

    private static async Task<(Archive Archive, byte[] Bytes)> Make(string name, int seed, int length = 4096)
    {
        var bytes = PreflightTestFolder.Bytes(seed, length);
        return (await PreflightTestFolder.ArchiveFor(name, bytes), bytes);
    }

    [Fact]
    public async Task CompleteFileIsMovedWithMetaAndCache()
    {
        var (archive, bytes) = await Make("SkyUI.7z", 1);
        var acquirer = await Started(new[] {archive});
        Assert.Equal(archive.Name, acquirer.Current?.Key);

        var source = await PreflightTestFolder.WriteFile(_folder.Watch, "SkyUI.7z", bytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
        Assert.False(source.FileExists(), "the source should have been moved, not copied");
        Assert.Null(acquirer.Current);
        Assert.Equal(new ManualDownloadCounts(1, 1, 0, 0, 0, 0), acquirer.Counts);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await acquirer.WaitForCompletion(cts.Token);
    }

    [Fact]
    public async Task FileAlreadyInTheWatchFolderAtStartIsPicked()
    {
        var (archive, bytes) = await Make("Existing.7z", 2);
        await PreflightTestFolder.WriteFile(_folder.Watch, "Existing.7z", bytes);

        var acquirer = await Started(new[] {archive});

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task GrowingFileIsNotVerifiedUntilClosed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (archive, bytes) = await Make("Growing.7z", 3, 64 * 1024);
        var acquirer = await Started(new[] {archive});

        var path = _folder.Watch.Combine("Growing.7z");
        await using (var stream = new FileStream(path.ToString(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            // Grow in steps; the last step lands on the final size while the handle is still open.
            var written = 0;
            while (written < bytes.Length)
            {
                var chunk = Math.Min(8 * 1024, bytes.Length - written);
                await stream.WriteAsync(bytes.AsMemory(written, chunk));
                await stream.FlushAsync();
                written += chunk;
                await Task.Delay(30);
            }

            await PreflightTestFolder.SettleTime();
            Assert.NotEqual(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);
            Assert.False(_folder.Destination.Combine("Growing.7z").FileExists());
        }

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task PreallocatedFileIsNotVerifiedUntilClosed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (archive, bytes) = await Make("Preallocated.7z", 4, 64 * 1024);
        var acquirer = await Started(new[] {archive});

        var path = _folder.Watch.Combine("Preallocated.7z");
        await using (var stream = new FileStream(path.ToString(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            // The final size is visible from the first moment, as Firefox, FDM and IDM do it.
            stream.SetLength(bytes.Length);
            await stream.FlushAsync();
            await PreflightTestFolder.SettleTime();

            var state = Item(acquirer, archive.Name).State;
            Assert.NotEqual(ManualDownloadState.Moved, state);
            Assert.NotEqual(ManualDownloadState.WrongFile, state);

            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
            await PreflightTestFolder.SettleTime();
            Assert.NotEqual(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);
        }

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task LockedFileWaitsThenMovesOnceFreed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (archive, bytes) = await Make("Locked.7z", 5);
        var acquirer = await Started(new[] {archive});
        var seen = new List<ManualDownloadState>();
        using var sub = acquirer.Events.Subscribe(e => { lock (seen) seen.Add(e.Item.State); });

        var path = await PreflightTestFolder.WriteFile(_folder.Watch, "Locked.7z", bytes);
        using (path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await WaitForState(acquirer, archive.Name, ManualDownloadState.Waiting);
            await PreflightTestFolder.SettleTime();
            Assert.Equal(ManualDownloadState.Waiting, Item(acquirer, archive.Name).State);
            Assert.Contains("still writing", Item(acquirer, archive.Name).Message);
        }

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
        lock (seen)
        {
            Assert.Contains(ManualDownloadState.Waiting, seen);
            Assert.True(seen.IndexOf(ManualDownloadState.Waiting) < seen.LastIndexOf(ManualDownloadState.Moved));
        }
    }

    [Fact]
    public async Task PartialExtensionIsIgnoredUntilRenamed()
    {
        var (archive, bytes) = await Make("Chrome.7z", 6);
        var acquirer = await Started(new[] {archive});

        var partial = await PreflightTestFolder.WriteFile(_folder.Watch, "Chrome.7z.crdownload", bytes);
        await PreflightTestFolder.SettleTime();
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);
        Assert.True(partial.FileExists());

        File.Move(partial.ToString(), _folder.Watch.Combine("Chrome.7z").ToString());

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task FirefoxPlaceholderIsIgnoredWhilePartExists()
    {
        var (archive, bytes) = await Make("Firefox.7z", 7);
        var acquirer = await Started(new[] {archive});

        // Firefox: the final name exists beside a .part that is still being written. Here the final name
        // already has the full content so that only the sibling rule can be what holds it back.
        var part = await PreflightTestFolder.WriteFile(_folder.Watch, "Firefox.7z.part", new byte[100]);
        await PreflightTestFolder.WriteFile(_folder.Watch, "Firefox.7z", bytes);
        await PreflightTestFolder.SettleTime();
        Assert.NotEqual(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);

        part.Delete();

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task WrongSizeIsIgnoredWithoutHashing()
    {
        var (archive, _) = await Make("Sized.7z", 8, 4096);
        var acquirer = await Started(new[] {archive});
        var events = 0;
        using var sub = acquirer.Events.Subscribe(_ => Interlocked.Increment(ref events));

        var wrong = await PreflightTestFolder.WriteFile(_folder.Watch, "Sized.7z", PreflightTestFolder.Bytes(8, 4095));
        await PreflightTestFolder.SettleTime();

        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);
        Assert.Equal(0, events);
        Assert.True(wrong.FileExists());
    }

    [Fact]
    public async Task RightSizeWrongHashIsWrongFileAndLeftAlone()
    {
        var (archive, _) = await Make("Version.7z", 9, 4096);
        var acquirer = await Started(new[] {archive});

        var wrongBytes = PreflightTestFolder.Bytes(99, 4096);
        var wrong = await PreflightTestFolder.WriteFile(_folder.Watch, "Version.7z", wrongBytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.WrongFile);
        var item = Item(acquirer, archive.Name);
        Assert.Equal(wrong, item.CandidatePath);
        Assert.Contains(archive.Hash.ToHex(), item.Message);
        Assert.Equal(1, acquirer.Counts.WrongFile);

        // Still there, untouched, and still watched: a later correct file completes the item.
        await PreflightTestFolder.SettleTime();
        Assert.True(wrong.FileExists());
        Assert.Equal(wrongBytes, await wrong.ReadAllBytesAsync());
        Assert.False(_folder.Destination.Combine("Version.7z").FileExists());
        Assert.Equal(ManualDownloadState.WrongFile, Item(acquirer, archive.Name).State);

        acquirer.Retry(archive.Name);
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);
        Assert.Null(Item(acquirer, archive.Name).CandidatePath);
        await PreflightTestFolder.SettleTime();
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);
    }

    [Fact]
    public async Task TwoSameSizeArchivesAreToldApartByHash()
    {
        var (a, aBytes) = await Make("A.7z", 10, 8192);
        var (b, bBytes) = await Make("B.7z", 11, 8192);
        var acquirer = await Started(new[] {a, b});

        // Landed under names that say nothing about which is which.
        await PreflightTestFolder.WriteFile(_folder.Watch, "download (1).7z", bBytes);
        await PreflightTestFolder.WriteFile(_folder.Watch, "download (2).7z", aBytes);

        await WaitForState(acquirer, a.Name, ManualDownloadState.Moved);
        await WaitForState(acquirer, b.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, a, aBytes);
        await AssertPlaced(acquirer, b, bBytes);
    }

    [Fact]
    public async Task IdenticalBytesUnderTwoNamesAreBothPlaced()
    {
        var bytes = PreflightTestFolder.Bytes(12, 8192);
        var first = await PreflightTestFolder.ArchiveFor("First.7z", bytes);
        var second = await PreflightTestFolder.ArchiveFor("Second.7z", bytes);
        var acquirer = await Started(new[] {first, second});

        await PreflightTestFolder.WriteFile(_folder.Watch, "First.7z", bytes);

        await WaitForState(acquirer, first.Name, ManualDownloadState.Moved);
        await WaitForState(acquirer, second.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, first, bytes);
        await AssertPlaced(acquirer, second, bytes);
        Assert.Null(acquirer.Current);
    }

    [Fact]
    public async Task FiveFilesAtOnceAreAllPlaced()
    {
        var made = new List<(Archive Archive, byte[] Bytes)>();
        for (var i = 0; i < 5; i++)
            made.Add(await Make($"Batch{i}.7z", 20 + i, 4096 + i * 512));
        var acquirer = await Started(made.Select(m => m.Archive));

        await Task.WhenAll(made.Select(m => PreflightTestFolder.WriteFile(_folder.Watch, m.Archive.Name, m.Bytes)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await acquirer.WaitForCompletion(cts.Token);
        foreach (var (archive, bytes) in made)
            await AssertPlaced(acquirer, archive, bytes);
        Assert.Equal(5, acquirer.Counts.Moved);
    }

    [Fact]
    public async Task ALaterItemCompletingDoesNotDisturbCurrent()
    {
        var (a, _) = await Make("Item1.7z", 30, 1024);
        var (b, _) = await Make("Item2.7z", 31, 2048);
        var (c, cBytes) = await Make("Item3.7z", 32, 3072);
        var acquirer = await Started(new[] {a, b, c});
        Assert.Equal(a.Name, acquirer.Current?.Key);

        await PreflightTestFolder.WriteFile(_folder.Watch, "Item3.7z", cBytes);

        await WaitForState(acquirer, c.Name, ManualDownloadState.Moved);
        Assert.Equal(a.Name, acquirer.Current?.Key);
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, a.Name).State);
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, b.Name).State);
        Assert.Equal(new[] {a.Name, b.Name, c.Name}, acquirer.Snapshot().Select(i => i.Key));
    }

    [Fact]
    public async Task SkipMovesTheItemToTheEnd()
    {
        var (a, _) = await Make("Skip1.7z", 40, 1024);
        var (b, _) = await Make("Skip2.7z", 41, 2048);
        var (c, _) = await Make("Skip3.7z", 42, 3072);
        var acquirer = await Started(new[] {a, b, c});
        ManualDownloadEvent? last = null;
        using var sub = acquirer.Events.Subscribe(e => last = e);

        acquirer.Skip(a.Name);

        Assert.Equal(b.Name, acquirer.Current?.Key);
        Assert.Equal(new[] {b.Name, c.Name, a.Name}, acquirer.Snapshot().Select(i => i.Key));
        Assert.NotNull(last);
        Assert.Equal(a.Name, last!.Item.Key);

        acquirer.Skip(b.Name);
        Assert.Equal(new[] {c.Name, a.Name, b.Name}, acquirer.Snapshot().Select(i => i.Key));
        Assert.Equal(c.Name, acquirer.Current?.Key);
    }

    [Fact]
    public async Task AddFileManuallyWithTheRightFilePlacesIt()
    {
        var (archive, bytes) = await Make("Picked.7z", 50);
        var acquirer = await Started(new[] {archive});

        var elsewhere = _folder.NewFolder("elsewhere");
        var picked = await PreflightTestFolder.WriteFile(elsewhere, "renamed-by-user.7z", bytes);

        await acquirer.AddFileManually(archive.Name, picked, CancellationToken.None);

        Assert.Equal(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task AddFileManuallyWithTheWrongFileExplainsWhy()
    {
        var (archive, _) = await Make("Picked.7z", 51, 4096);
        var acquirer = await Started(new[] {archive});
        var elsewhere = _folder.NewFolder("elsewhere");

        var wrongSize = await PreflightTestFolder.WriteFile(elsewhere, "small.7z", PreflightTestFolder.Bytes(1, 100));
        await acquirer.AddFileManually(archive.Name, wrongSize, CancellationToken.None);
        var item = Item(acquirer, archive.Name);
        Assert.Equal(ManualDownloadState.WrongFile, item.State);
        Assert.Equal(wrongSize, item.CandidatePath);
        Assert.Contains("bytes", item.Message);
        Assert.Contains("Wrong file", item.Message);

        var wrongHash = await PreflightTestFolder.WriteFile(elsewhere, "other.7z", PreflightTestFolder.Bytes(2, 4096));
        await acquirer.AddFileManually(archive.Name, wrongHash, CancellationToken.None);
        item = Item(acquirer, archive.Name);
        Assert.Equal(ManualDownloadState.WrongFile, item.State);
        Assert.Equal(wrongHash, item.CandidatePath);
        Assert.Contains("not the right file", item.Message);

        Assert.True(wrongSize.FileExists());
        Assert.True(wrongHash.FileExists());
        Assert.False(_folder.Destination.Combine("Picked.7z").FileExists());
    }

    [Fact]
    public async Task AddFileManuallyMatchingAnotherItemPlacesThatOne()
    {
        var (asked, _) = await Make("Asked.7z", 52, 4096);
        var (other, otherBytes) = await Make("Other.7z", 53, 4096);
        var acquirer = await Started(new[] {asked, other});
        var elsewhere = _folder.NewFolder("elsewhere");

        var picked = await PreflightTestFolder.WriteFile(elsewhere, "mystery.7z", otherBytes);
        await acquirer.AddFileManually(asked.Name, picked, CancellationToken.None);

        await AssertPlaced(acquirer, other, otherBytes);
        var item = Item(acquirer, asked.Name);
        Assert.Equal(ManualDownloadState.Pending, item.State);
        Assert.Contains("Other.7z", item.Message);
        Assert.Contains("still needed", item.Message);
        Assert.Equal(asked.Name, acquirer.Current?.Key);
    }

    [Fact]
    public async Task ASecondCopyAfterMovedIsIgnoredAndKept()
    {
        var (archive, bytes) = await Make("Dup.7z", 60);
        var acquirer = await Started(new[] {archive});
        var notices = new List<ManualDownloadNotice>();
        using var sub = acquirer.Notices.Subscribe(n => { lock (notices) notices.Add(n); });

        await PreflightTestFolder.WriteFile(_folder.Watch, "Dup.7z", bytes);
        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);

        var duplicate = await PreflightTestFolder.WriteFile(_folder.Watch, "Dup (1).7z", bytes);

        await PreflightTestFolder.WaitUntil(() =>
        {
            lock (notices) return notices.Any(n => n.Kind == ManualDownloadNoticeKind.DuplicateIgnored);
        }, "a DuplicateIgnored notice");

        await PreflightTestFolder.SettleTime();
        Assert.True(duplicate.FileExists(), "the user's duplicate must not be deleted");
        lock (notices)
        {
            var dup = notices.Where(n => n.Kind == ManualDownloadNoticeKind.DuplicateIgnored).ToList();
            Assert.Single(dup);
            Assert.Equal(duplicate, dup[0].Path);
        }

        Assert.Equal(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task AlreadyCorrectInDestinationIsMovedAtStartWithoutTouchingAnything()
    {
        var (archive, bytes) = await Make("Present.7z", 61);
        var dest = await PreflightTestFolder.WriteFile(_folder.Destination, "Present.7z", bytes);
        var stamp = dest.LastModifiedUtc();
        var watched = await PreflightTestFolder.WriteFile(_folder.Watch, "Present.7z", bytes);

        var acquirer = await Started(new[] {archive});

        var item = Item(acquirer, archive.Name);
        Assert.Equal(ManualDownloadState.Moved, item.State);
        Assert.Contains("already present", item.Message);
        Assert.Equal(dest, item.PlacedPath);
        Assert.Equal(stamp, dest.LastModifiedUtc());
        await AssertPlaced(acquirer, archive, bytes);

        await PreflightTestFolder.SettleTime();
        Assert.True(watched.FileExists(), "the copy in the watch folder is the user's and stays");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await acquirer.WaitForCompletion(cts.Token);
    }

    [Fact]
    public async Task StaleFileInDestinationIsReplaced()
    {
        var (archive, bytes) = await Make("Stale.7z", 62, 4096);
        var dest = await PreflightTestFolder.WriteFile(_folder.Destination, "Stale.7z", PreflightTestFolder.Bytes(999, 4096));

        var acquirer = await Started(new[] {archive});
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);

        var source = await PreflightTestFolder.WriteFile(_folder.Watch, "Stale.7z", bytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
        Assert.Equal(bytes, await dest.ReadAllBytesAsync());
        Assert.False(source.FileExists());
    }

    [Fact]
    public async Task WatchFolderMayBeTheDestination()
    {
        var (named, namedBytes) = await Make("Named.7z", 63, 4096);
        var (other, otherBytes) = await Make("Other.7z", 64, 5120);
        var acquirer = await Started(new[] {named, other}, watch: _folder.Destination);

        await PreflightTestFolder.WriteFile(_folder.Destination, "Named.7z", namedBytes);
        await PreflightTestFolder.WriteFile(_folder.Destination, "download.7z", otherBytes);

        await WaitForState(acquirer, named.Name, ManualDownloadState.Moved);
        await WaitForState(acquirer, other.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, named, namedBytes);
        await AssertPlaced(acquirer, other, otherBytes);
        Assert.False(_folder.Destination.Combine("download.7z").FileExists());

        // Our own placed files and meta files are seen by the watcher too; they must not become duplicates.
        var notices = new List<ManualDownloadNotice>();
        using var sub = acquirer.Notices.Subscribe(n => { lock (notices) notices.Add(n); });
        await PreflightTestFolder.SettleTime();
        await PreflightTestFolder.SettleTime();
        lock (notices) Assert.DoesNotContain(notices, n => n.Kind == ManualDownloadNoticeKind.DuplicateIgnored);
        Assert.Empty(_folder.Destination.EnumerateFiles(Ext.WjIncoming, false));
    }

    [Fact]
    public async Task CancellationMidCopyLeavesNothingInTheDestination()
    {
        var (archive, bytes) = await Make("Big.7z", 70, 16 * 1024 * 1024);
        // Throttled so the copy is still running when the token fires.
        var slow = new Resource<FileHashCache>("Slow hashing", 2, 4 * 1024 * 1024);
        var acquirer = NewAcquirer(FastOptions(forceCopy: true), slow);
        using var cts = new CancellationTokenSource();
        await acquirer.Start(new[] {archive}, _folder.Watch, _folder.Destination, cts.Token);

        using var sub = acquirer.Events
            .Where(e => e.Item.State == ManualDownloadState.Verifying && e.Item.Message != null &&
                        e.Item.Message.Contains("moving it into place") && e.Item.Progress.Value > 0 &&
                        e.Item.Progress.Value < 1)
            .Take(1)
            .Subscribe(_ => cts.Cancel());

        var source = await PreflightTestFolder.WriteFile(_folder.Watch, "Big.7z", bytes);

        await PreflightTestFolder.WaitUntil(() => cts.IsCancellationRequested, "the copy to start", TimeSpan.FromSeconds(30));
        await acquirer.Stop();

        var item = Item(acquirer, archive.Name);
        Assert.Equal(ManualDownloadState.Pending, item.State);
        Assert.Equal(source, item.CandidatePath);
        Assert.True(source.FileExists(), "the source is left untouched");
        Assert.False(_folder.Destination.Combine("Big.7z").FileExists());
        Assert.Empty(_folder.Destination.EnumerateFiles(Ext.WjIncoming, false));
        Assert.Empty(_folder.Destination.EnumerateFiles("*", false));
    }

    [Fact]
    public async Task StaleIncomingFilesAreSweptAtStart()
    {
        var (archive, _) = await Make("Any.7z", 71);
        var stale = await PreflightTestFolder.WriteFile(_folder.Destination, "abc123.wj_incoming", new byte[10]);
        var unrelated = await PreflightTestFolder.WriteFile(_folder.Destination, "keep.7z", new byte[10]);

        await Started(new[] {archive});

        Assert.False(stale.FileExists());
        Assert.True(unrelated.FileExists());
    }

    [Fact]
    public async Task AnEventStormDoesNotLoseTheFile()
    {
        var (archive, bytes) = await Make("Storm.7z", 72);
        var acquirer = await Started(new[] {archive});

        var noise = _folder.Watch;
        var writes = Enumerable.Range(0, 400)
            .Select(i => PreflightTestFolder.WriteFile(noise, $"noise{i}.txt", new byte[16]))
            .ToList();
        writes.Add(PreflightTestFolder.WriteFile(noise, "Storm.7z", bytes));
        await Task.WhenAll(writes);
        foreach (var i in Enumerable.Range(0, 400))
            File.Delete(noise.Combine($"noise{i}.txt").ToString());

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task PollOnlyModeFindsFiles()
    {
        var (archive, bytes) = await Make("Polled.7z", 73);
        var acquirer = await Started(new[] {archive}, FastOptions(watcher: false));

        await PreflightTestFolder.WriteFile(_folder.Watch, "Polled.7z", bytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task WatcherOnlyModeFindsFiles()
    {
        var (archive, bytes) = await Make("Watched.7z", 74);
        var acquirer = await Started(new[] {archive}, FastOptions(poll: Timeout.InfiniteTimeSpan));
        await Task.Delay(300);

        await PreflightTestFolder.WriteFile(_folder.Watch, "Watched.7z", bytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task AMissingWatchFolderIsReportedAndRecoveredBySetWatchFolder()
    {
        var (archive, bytes) = await Make("Relocated.7z", 75);
        var missing = _folder.Root.Combine("does-not-exist");
        var acquirer = NewAcquirer();
        var notices = new List<ManualDownloadNotice>();
        using var sub = acquirer.Notices.Subscribe(n => { lock (notices) notices.Add(n); });

        await acquirer.Start(new[] {archive}, missing, _folder.Destination, CancellationToken.None);
        Assert.Equal(missing, acquirer.WatchFolder);

        await PreflightTestFolder.WaitUntil(() =>
        {
            lock (notices) return notices.Any(n => n.Kind == ManualDownloadNoticeKind.WatchFolderUnavailable);
        }, "a WatchFolderUnavailable notice");

        await PreflightTestFolder.WriteFile(_folder.Watch, "Relocated.7z", bytes);
        acquirer.SetWatchFolder(_folder.Watch);
        Assert.Equal(_folder.Watch, acquirer.WatchFolder);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
        lock (notices)
        {
            Assert.Contains(notices, n => n.Kind == ManualDownloadNoticeKind.WatchFolderChanged && n.Path == _folder.Watch);
            Assert.Single(notices, n => n.Kind == ManualDownloadNoticeKind.WatchFolderUnavailable);
        }
    }

    [Fact]
    public async Task AMissingWatchFolderThatAppearsLaterIsPickedUp()
    {
        var (archive, bytes) = await Make("Late.7z", 76);
        var late = _folder.Root.Combine("late");
        var acquirer = await Started(new[] {archive}, watch: late);

        await Task.Delay(300);
        late.CreateDirectory();
        await PreflightTestFolder.WriteFile(late, "Late.7z", bytes);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task AnArchiveWithoutAHashIsRejected()
    {
        var (archive, _) = await Make("NoHash.7z", 77);
        archive.Hash = default;
        var acquirer = NewAcquirer();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            acquirer.Start(new[] {archive}, _folder.Watch, _folder.Destination, CancellationToken.None));
        Assert.Contains("NoHash.7z", ex.Message);
    }

    [Fact]
    public async Task AnEmptyQueueIsCompleteImmediately()
    {
        var acquirer = await Started(Array.Empty<Archive>());
        Assert.Null(acquirer.Current);
        Assert.Equal(new ManualDownloadCounts(0, 0, 0, 0, 0, 0), acquirer.Counts);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await acquirer.WaitForCompletion(cts.Token);
    }

    [Fact]
    public async Task RescanPicksUpAFilePlacedInTheDestinationByHand()
    {
        var (archive, bytes) = await Make("ByHand.7z", 78);
        var acquirer = await Started(new[] {archive}, FastOptions(watcher: false, poll: Timeout.InfiniteTimeSpan));

        await PreflightTestFolder.WriteFile(_folder.Destination, "ByHand.7z", bytes);
        await PreflightTestFolder.SettleTime();
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);

        await acquirer.Rescan(CancellationToken.None);

        Assert.Equal(ManualDownloadState.Moved, Item(acquirer, archive.Name).State);
        await AssertPlaced(acquirer, archive, bytes);
    }

    [Fact]
    public async Task EventsCarryTheTransitionAndCounts()
    {
        var (archive, bytes) = await Make("Events.7z", 79);
        var acquirer = await Started(new[] {archive});
        var events = new List<ManualDownloadEvent>();
        using var sub = acquirer.Events.Subscribe(e => { lock (events) events.Add(e); });

        await PreflightTestFolder.WriteFile(_folder.Watch, "Events.7z", bytes);
        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);

        lock (events)
        {
            Assert.Equal(ManualDownloadState.Pending, events[0].Previous);
            Assert.Equal(ManualDownloadState.Detected, events[0].Item.State);
            var final = events[^1];
            Assert.Equal(ManualDownloadState.Moved, final.Item.State);
            Assert.Equal(1, final.Counts.Moved);
            Assert.Equal(0, final.Counts.InProgress);
            Assert.All(events, e => Assert.Equal(archive.Name, e.Item.Key));
        }
    }

    [Fact]
    public async Task StopJoinsAnInFlightAddFileManually()
    {
        var (archive, bytes) = await Make("Slow.7z", 80, 16 * 1024 * 1024);
        // Throttled so the hash is still running when Stop is called.
        var slow = new Resource<FileHashCache>("Slow hashing", 2, 4 * 1024 * 1024);
        var acquirer = NewAcquirer(FastOptions(watcher: false, poll: Timeout.InfiniteTimeSpan), slow);
        await acquirer.Start(new[] {archive}, _folder.Watch, _folder.Destination, CancellationToken.None);

        var picked = await PreflightTestFolder.WriteFile(_folder.NewFolder("elsewhere"), "Slow.7z", bytes);
        var add = acquirer.AddFileManually(archive.Name, picked, CancellationToken.None);

        await PreflightTestFolder.WaitUntil(() =>
        {
            var item = Item(acquirer, archive.Name);
            return item.State == ManualDownloadState.Verifying && item.Progress.Value > 0;
        }, "the hash to be under way", TimeSpan.FromSeconds(30));

        await acquirer.Stop();
        Assert.True(add.IsCompleted, "Stop returned while AddFileManually was still working");
        await acquirer.DisposeAsync();

        // Cancelled by Stop, or finished just before it; never an ObjectDisposedException.
        try
        {
            await add;
        }
        catch (OperationCanceledException)
        {
        }

        var final = Item(acquirer, archive.Name);
        Assert.True(final.State is ManualDownloadState.Pending or ManualDownloadState.Moved, final.State.ToString());
        Assert.Empty(_folder.Destination.EnumerateFiles(Ext.WjIncoming, false));
    }

    [Fact]
    public async Task PublishAfterDisposeIsIgnored()
    {
        var (a, _) = await Make("Late1.7z", 81, 1024);
        var (b, _) = await Make("Late2.7z", 82, 2048);
        var acquirer = await Started(new[] {a, b});
        var completed = false;
        using var sub = acquirer.Events.Subscribe(_ => { }, () => completed = true);

        await acquirer.DisposeAsync();
        Assert.True(completed);

        // Both publish when live; after disposal the state still changes and nothing is said.
        acquirer.Skip(a.Name);
        Assert.Equal(b.Name, acquirer.Current?.Key);

        var wrong = await PreflightTestFolder.WriteFile(_folder.NewFolder("elsewhere"), "wrong.7z", new byte[10]);
        await acquirer.AddFileManually(a.Name, wrong, CancellationToken.None);
        Assert.Equal(ManualDownloadState.WrongFile, Item(acquirer, a.Name).State);

        await acquirer.DisposeAsync();
    }

    [Fact]
    public async Task AddFileManuallyWithAFileOfAnotherItemsSizeExplainsWhy()
    {
        var (asked, _) = await Make("Asked.7z", 83, 4096);
        var (other, _) = await Make("Other.7z", 84, 8192);
        var acquirer = await Started(new[] {asked, other});

        // Other's size, nobody's hash.
        var picked = await PreflightTestFolder.WriteFile(_folder.NewFolder("elsewhere"), "mystery.7z",
            PreflightTestFolder.Bytes(985, 8192));
        await acquirer.AddFileManually(asked.Name, picked, CancellationToken.None);

        var item = Item(acquirer, asked.Name);
        Assert.Equal(ManualDownloadState.WrongFile, item.State);
        Assert.Equal(picked, item.CandidatePath);
        Assert.Contains("matches nothing", item.Message);
        Assert.Contains($"{asked.Size:N0}", item.Message);
        Assert.Contains(asked.Hash.ToHex(), item.Message);

        // The size sibling saw a file of its size that was not it, as before.
        Assert.Equal(ManualDownloadState.WrongFile, Item(acquirer, other.Name).State);
        Assert.True(picked.FileExists());
        Assert.Empty(_folder.Destination.EnumerateFiles("*", false));
    }

    [Fact]
    public async Task CompletedWorkItemsAreNotRetained()
    {
        var (archive, _) = await Make("Churn.7z", 85, 4096);
        var acquirer = await Started(new[] {archive}, FastOptions(watcher: false, poll: Timeout.InfiniteTimeSpan));

        // Right size, wrong hash: hashed once, then every later look is a cache hit that finishes at once.
        await PreflightTestFolder.WriteFile(_folder.Watch, "Churn.7z", PreflightTestFolder.Bytes(986, 4096));
        await acquirer.Rescan(CancellationToken.None);
        await WaitForState(acquirer, archive.Name, ManualDownloadState.WrongFile);

        for (var i = 0; i < 300; i++)
            await acquirer.Rescan(CancellationToken.None);

        await PreflightTestFolder.WaitUntil(() => acquirer.WorkInFlight == 0, "the work list to drain");
        await PreflightTestFolder.SettleTime();
        Assert.Equal(0, acquirer.WorkInFlight);
        Assert.Equal(ManualDownloadState.WrongFile, Item(acquirer, archive.Name).State);
    }

    [Fact]
    public async Task WaitingIsPublishedOnce()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (archive, bytes) = await Make("Held.7z", 86);
        var acquirer = await Started(new[] {archive});
        var waiting = 0;
        using var sub = acquirer.Events.Subscribe(e =>
        {
            if (e.Item.State == ManualDownloadState.Waiting) Interlocked.Increment(ref waiting);
        });

        var path = await PreflightTestFolder.WriteFile(_folder.Watch, "Held.7z", bytes);
        using (path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await WaitForState(acquirer, archive.Name, ManualDownloadState.Waiting);
            // Long enough for a dozen lock retries at the fast timings.
            await PreflightTestFolder.SettleTime();
            await PreflightTestFolder.SettleTime();
        }

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        Assert.Equal(1, waiting);
    }

    [Fact]
    public async Task AFailedPlacementIsRetriedFromTheSameCandidate()
    {
        var (archive, bytes) = await Make("Blocked.7z", 87);
        var acquirer = await Started(new[] {archive}, FastOptions(watcher: false, poll: Timeout.InfiniteTimeSpan));

        // A folder squatting on the destination name makes the placement fail where a full disk would:
        // after verification, in the move. A full disk itself cannot be arranged on a developer machine.
        var squatter = _folder.Destination.Combine("Blocked.7z");
        squatter.CreateDirectory();
        var source = await PreflightTestFolder.WriteFile(_folder.Watch, "Blocked.7z", bytes);
        await acquirer.Rescan(CancellationToken.None);

        await WaitForState(acquirer, archive.Name, ManualDownloadState.Failed);
        var failed = Item(acquirer, archive.Name);
        Assert.Contains("Could not move", failed.Message);
        Assert.Equal(source, failed.CandidatePath);
        Assert.Equal(1, acquirer.Counts.Failed);
        Assert.True(source.FileExists());
        Assert.Equal(archive.Name, acquirer.Current?.Key);

        Directory.Delete(squatter.ToString());
        acquirer.Retry(archive.Name);
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);

        // Nothing polls here, so only the requeued candidate can complete it.
        await WaitForState(acquirer, archive.Name, ManualDownloadState.Moved);
        await AssertPlaced(acquirer, archive, bytes);
        Assert.False(source.FileExists());
    }

    [Fact]
    public async Task CloudPlaceholderProducesANotice()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (archive, bytes) = await Make("Cloud.7z", 88);
        var acquirer = await Started(new[] {archive});
        var notices = new List<ManualDownloadNotice>();
        var events = 0;
        using var noticeSub = acquirer.Notices.Subscribe(n => { lock (notices) notices.Add(n); });
        using var eventSub = acquirer.Events.Subscribe(_ => Interlocked.Increment(ref events));

        // Marked offline before it is in the watched folder, so the acquirer only ever sees a placeholder.
        var staged = await PreflightTestFolder.WriteFile(_folder.NewFolder("staging"), "Cloud.7z", bytes);
        File.SetAttributes(staged.ToString(), File.GetAttributes(staged.ToString()) | FileAttributes.Offline);
        var path = _folder.Watch.Combine("Cloud.7z");
        File.Move(staged.ToString(), path.ToString());

        await PreflightTestFolder.WaitUntil(() =>
        {
            lock (notices) return notices.Any(n => n.Kind == ManualDownloadNoticeKind.CloudPlaceholder);
        }, "a CloudPlaceholder notice");
        await PreflightTestFolder.SettleTime();

        lock (notices)
        {
            var cloud = Assert.Single(notices, n => n.Kind == ManualDownloadNoticeKind.CloudPlaceholder);
            Assert.Equal(path, cloud.Path);
            Assert.Contains("Always keep on this device", cloud.Message);
        }

        // Never opened: an open is always preceded by a Detected event, and the item was never told anything.
        Assert.Equal(0, events);
        Assert.Equal(ManualDownloadState.Pending, Item(acquirer, archive.Name).State);
        Assert.True(path.FileExists());
        Assert.False(_folder.Destination.Combine("Cloud.7z").FileExists());
    }
}
