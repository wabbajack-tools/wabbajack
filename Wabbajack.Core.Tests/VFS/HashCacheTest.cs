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