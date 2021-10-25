using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.VFS.Test;

public class HashCacheTest
{
    private readonly FileHashCache _cache;
    private readonly TemporaryFileManager _manager;

    public HashCacheTest(FileHashCache cache, TemporaryFileManager manager)
    {
        _cache = cache;
        _manager = manager;
    }


    [Fact]
    public async Task CanCacheAndPurgeHashes()
    {
        var testFile = _manager.CreateFile();
        await testFile.Path.WriteAllTextAsync("Cheese for Everyone!");

        Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="),
            await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None));
        Assert.True(_cache.TryGetHashCache(testFile.Path, out var hash));

        _cache.Purge(testFile.Path);
        Assert.False(_cache.TryGetHashCache(testFile.Path, out _));
        Assert.Equal(hash, await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None));

        Assert.True(_cache.TryGetHashCache(testFile.Path, out _));

        _cache.VacuumDatabase();
    }
}