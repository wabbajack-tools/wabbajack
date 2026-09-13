using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     The cache holds one SQLite connection and is called from every hashing thread at once, while a purge
///     or vacuum can arrive from another thread in the middle of a lookup. A VACUUM cannot start while a
///     reader on the same connection is still open: without the lock in the cache this fails every run, in
///     VacuumDatabase, with "SQL logic error".
/// </summary>
public class FileHashCacheConcurrencyTests : IDisposable
{
    private const int Workers = 16;
    private const int IterationsPerWorker = 2000;
    private const int FileCount = 64;

    private readonly TemporaryFileManager _manager;

    public FileHashCacheConcurrencyTests()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    [Fact]
    public async Task LookupsPurgesAndVacuumsFromManyThreadsDoNotInterfere()
    {
        var cache = new FileHashCache(_manager.CreateFolder().Path.Combine("hashcache.sqlite"),
            new Resource<FileHashCache>("Test hashing", Workers));

        var files = new AbsolutePath[FileCount];
        var expected = new Hash[FileCount];
        for (var i = 0; i < FileCount; i++)
        {
            files[i] = _manager.CreateFile().Path;
            await files[i].WriteAllTextAsync($"Cheese for Everyone! {i}");
            expected[i] = await files[i].Hash(CancellationToken.None);
        }

        var workers = Enumerable.Range(0, Workers).Select(worker => Task.Run(async () =>
        {
            var rng = new Random(worker);
            for (var i = 0; i < IterationsPerWorker; i++)
            {
                var idx = rng.Next(FileCount);
                switch (i % 8)
                {
                    case 3:
                        cache.Purge(files[idx]);
                        break;
                    case 5 when worker == 0:
                        cache.VacuumDatabase();
                        break;
                    default:
                        Assert.Equal(expected[idx],
                            await cache.FileHashCachedAsync(files[idx], CancellationToken.None));
                        break;
                }
            }
        })).ToArray();

        await Task.WhenAll(workers);

        for (var i = 0; i < FileCount; i++)
            Assert.Equal(expected[i], await cache.FileHashCachedAsync(files[i], CancellationToken.None));
    }
}
