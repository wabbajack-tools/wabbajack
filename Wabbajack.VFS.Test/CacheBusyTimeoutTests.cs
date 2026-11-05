using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     The caches live in one place per user, so the app and the CLI, or two copies of the app, share the
///     same files. Opening a cache while another process is writing to it must not fail: it used to fail at
///     once, which failed an install before it started with "database is locked". In WAL mode the open no
///     longer needs the lock at all; a write still does, and has to wait for it rather than fail.
/// </summary>
public class CacheBusyTimeoutTests
{
    private static readonly TimeSpan HeldFor = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Kept beside the test binaries rather than in a per-test temporary folder: the caches never close
    ///     their connection, which holds up deleting the folder. A file left by an earlier run is fine.
    /// </summary>
    private static AbsolutePath Database(string name)
    {
        return KnownFolders.EntryPoint.Combine($"busy_timeout_{name}.sqlite");
    }

    /// <summary>Holds an exclusive lock on the database from another connection, as a writer elsewhere would.</summary>
    private static async Task<Task> HoldExclusiveLock(AbsolutePath database)
    {
        // Asynchronous continuations, or the test's blocking open would run on this thread and the lock
        // would never be let go.
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Task.Run(async () =>
        {
            await using var conn = new SQLiteConnection($"URI=file:{database};Pooling=False;");
            await conn.OpenAsync();
            await using (var begin = new SQLiteCommand("BEGIN EXCLUSIVE", conn))
                await begin.ExecuteNonQueryAsync();
            locked.SetResult();
            await Task.Delay(HeldFor);
            await using var commit = new SQLiteCommand("COMMIT", conn);
            await commit.ExecuteNonQueryAsync();
        });
        await locked.Task;
        return holder;
    }

    [Fact]
    public async Task VfsCacheOpensWhileAnotherWriterHoldsTheLock()
    {
        var database = Database("vfs");
        _ = new VFSDiskCache(database);

        var holder = await HoldExclusiveLock(database);
        _ = new VFSDiskCache(database);
        await holder;
    }

    [Fact]
    public async Task HashCacheOpensWhileAnotherWriterHoldsTheLock()
    {
        var database = Database("hash");
        _ = new FileHashCache(database, new Resource<FileHashCache>("Test hashing", 2));

        var holder = await HoldExclusiveLock(database);
        _ = new FileHashCache(database, new Resource<FileHashCache>("Test hashing", 2));
        await holder;
    }

    [Fact]
    public async Task HashCacheWriteWaitsForAnotherWriterInsteadOfFailing()
    {
        var database = Database("hash_write");
        var cache = new FileHashCache(database, new Resource<FileHashCache>("Test hashing", 2));
        // A file written here, not one of the test binaries: a binary copied into the output folder can have a
        // creation time later than its write time, which the cache repairs by touching the file, and the row
        // written below then no longer matches it.
        var file = KnownFolders.EntryPoint.Combine("busy_timeout_hash_write.txt");
        await file.WriteAllTextAsync(Guid.NewGuid().ToString());

        var holder = await HoldExclusiveLock(database);
        var timer = Stopwatch.StartNew();
        await cache.FileHashWriteCache(file, new Hashing.xxHash64.Hash(42));

        Assert.True(timer.Elapsed >= HeldFor - TimeSpan.FromMilliseconds(500), "wrote while the lock was still held");
        Assert.Equal(new Hashing.xxHash64.Hash(42), await cache.TryGetHashCache(file));
        await holder;
    }
}
