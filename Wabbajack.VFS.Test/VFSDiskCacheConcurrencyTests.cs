using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     Indexing analyzes archives in parallel, and every one of them reads and writes this cache while an
///     AddRoots finishing on another thread runs Clean, which is a VACUUM. All of it goes through one SQLite
///     connection, and a VACUUM cannot start while another statement on it is still open: without the lock in
///     the cache this fails every run, in Clean, with "SQL logic error".
/// </summary>
public class VFSDiskCacheConcurrencyTests : IDisposable
{
    private const int Workers = 16;
    private const int IterationsPerWorker = 2000;

    private readonly TemporaryFileManager _manager;

    public VFSDiskCacheConcurrencyTests()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    private static IndexedVirtualFile Entry(int worker, int i)
    {
        return new IndexedVirtualFile
        {
            Name = (RelativePath) $"worker{worker}/file{i}.txt",
            Hash = Hash.FromLong(worker * 1_000_000L + i + 1),
            Size = i,
            Children =
            {
                new IndexedVirtualFile
                {
                    Name = (RelativePath) "inner.txt",
                    Hash = Hash.FromLong(worker * 1_000_000L + i + 500_000),
                    Size = 1
                }
            }
        };
    }

    [Fact]
    public async Task PutsGetsAndCleansFromManyThreadsDoNotInterfere()
    {
        var cache = new VFSDiskCache(_manager.CreateFolder().Path.Combine("vfscache.sqlite"));

        var workers = Enumerable.Range(0, Workers).Select(worker => Task.Run(async () =>
        {
            for (var i = 0; i < IterationsPerWorker; i++)
            {
                var entry = Entry(worker, i);
                await cache.Put(entry, CancellationToken.None);

                var found = await cache.Get(entry.Hash, null!, CancellationToken.None);
                Assert.NotNull(found);
                Assert.Equal(entry.Hash, found!.Hash);
                Assert.Single(found.Children);

                // Re-putting an existing hash hits the primary key and is meant to be a quiet no-op.
                await cache.Put(Entry(worker, i), CancellationToken.None);

                if (worker == 0 && i % 32 == 5)
                    await cache.Clean();
            }
        })).ToArray();

        await Task.WhenAll(workers);
    }
}
