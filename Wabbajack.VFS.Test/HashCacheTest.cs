using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.VFS.Test;

public class HashCacheTest : IDisposable
{
    private readonly FileHashCache _cache;
    private readonly TemporaryFileManager _manager;

    public HashCacheTest(FileHashCache cache)
    {
        _cache = cache;
        // Its own temporary root: the registered TemporaryFileManager is a singleton, and another class
        // disposing it would delete the file this test is hashing.
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
    }

    public void Dispose()
    {
        _manager.Dispose();
    }


    [Fact]
    public async Task CanCacheAndPurgeHashes()
    {
        var testFile = _manager.CreateFile();
        await testFile.Path.WriteAllTextAsync("Cheese for Everyone!");

        Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="),
            await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None));
        Assert.True(await _cache.TryGetHashCache(testFile.Path) != default);

        _cache.Purge(testFile.Path);
        var hash = await testFile.Path.Hash(CancellationToken.None);
        Assert.NotEqual(hash, default);
        Assert.NotEqual(hash, await _cache.TryGetHashCache(testFile.Path));
        Assert.Equal(hash, await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None));

        Assert.Equal(hash, await _cache.TryGetHashCache(testFile.Path));

        _cache.VacuumDatabase();
    }
}