using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     End to end cover for reporting damaged archives. Extraction always visited every archive, but only
///     one failure reached the caller, so a user with three bad downloads was told about one, deleted it,
///     ran again, and was told about the next.
/// </summary>
public class CorruptArchiveReportingTests : IDisposable
{
    private readonly Context _context;
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _testDir;

    public CorruptArchiveReportingTests(Context context)
    {
        // Context and TemporaryFileManager are singletons, AddRoot replaces Context.Index, and disposing the
        // shared manager deletes the folder every other class is working in. Test classes run in parallel,
        // so this owns both: its own temporary root, and its own Context over that root.
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        _context = context.WithTemporaryFileManager(_manager);
        _testDir = _manager.CreateFolder();
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    private async Task<AbsolutePath> MakeZip(string name, string content)
    {
        var staging = _testDir.Combine($"{name}-staging");
        staging.CreateDirectory();
        await staging.Combine("payload.txt").WriteAllTextAsync(content);

        var zip = _testDir.Combine($"{name}.zip");
        ZipFile.CreateFromDirectory(staging.ToString(), zip.ToString());
        staging.DeleteDirectory();
        return zip;
    }

    /// <summary>
    ///     Damages an archive after it has been indexed, leaving its length alone so the only way to notice is
    ///     to read it. This is what the installer hits when a download went bad after it was verified.
    /// </summary>
    private static async Task CorruptInPlace(AbsolutePath archive)
    {
        var length = (int) archive.Size();

        // Zeroed rather than partially scrambled: a partly damaged zip can still be recovered far enough for
        // 7-Zip to produce the requested entry, which would make this test depend on how forgiving the
        // extractor happens to be. The length is preserved so nothing notices from the outside.
        await archive.WriteAllBytesAsync(new byte[length]);
    }

    [Fact]
    public async Task EveryCorruptArchiveIsReportedInOnePass()
    {
        var good = await MakeZip("good", "this one is fine");
        var first = await MakeZip("first", "this one gets damaged");
        var second = await MakeZip("second", "so does this one");

        await _context.AddRoot(_testDir, CancellationToken.None);

        var files = new[] {good, first, second}
            .Select(a => _context.Index.ByRootPath[a])
            .SelectMany(a => a.Children)
            .ToHashSet();

        await CorruptInPlace(first);
        await CorruptInPlace(second);

        var ex = await Assert.ThrowsAsync<ExtractionFailedException>(async () =>
            await _context.Extract(files, (_, _) => ValueTask.CompletedTask, CancellationToken.None));

        Assert.Equal(2, ex.CorruptArchives.Count);
        Assert.Contains(first, ex.CorruptArchives);
        Assert.Contains(second, ex.CorruptArchives);
        Assert.DoesNotContain(good, ex.CorruptArchives);

        Assert.Contains(first.ToString(), ex.Message);
        Assert.Contains(second.ToString(), ex.Message);
    }

    /// <summary>
    ///     The undamaged archives in the same pass still have their callback run, so one bad download does not
    ///     stop the rest of the work being done.
    /// </summary>
    [Fact]
    public async Task UndamagedArchivesAreStillExtracted()
    {
        var good = await MakeZip("good", "this one is fine");
        var bad = await MakeZip("bad", "this one gets damaged");

        await _context.AddRoot(_testDir, CancellationToken.None);

        var files = new[] {good, bad}
            .Select(a => _context.Index.ByRootPath[a])
            .SelectMany(a => a.Children)
            .ToHashSet();

        await CorruptInPlace(bad);

        var extracted = 0;

        await Assert.ThrowsAsync<ExtractionFailedException>(async () =>
            await _context.Extract(files, async (_, factory) =>
            {
                await using var s = await factory.GetStream();
                using var sr = new StreamReader(s);
                await sr.ReadToEndAsync();
                Interlocked.Increment(ref extracted);
            }, CancellationToken.None));

        Assert.Equal(1, extracted);
    }

    [Fact]
    public async Task AnUndamagedRunReportsNothing()
    {
        var first = await MakeZip("first", "fine");
        var second = await MakeZip("second", "also fine");

        await _context.AddRoot(_testDir, CancellationToken.None);

        var files = new[] {first, second}
            .Select(a => _context.Index.ByRootPath[a])
            .SelectMany(a => a.Children)
            .ToHashSet();

        var extracted = 0;
        await _context.Extract(files, (_, _) =>
        {
            Interlocked.Increment(ref extracted);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(2, extracted);
    }
}
