using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;

namespace Wabbajack.VFS.Test;

[ClassConstructor<VfsClassConstructor>]
public class HashCacheTest
{
    private readonly FileHashCache _cache;
    private readonly TemporaryFileManager _manager;

    public HashCacheTest(FileHashCache cache, TemporaryFileManager manager)
    {
        _cache = cache;
        _manager = manager;
    }


    [Test]
    public async Task CanCacheAndPurgeHashes()
    {
        var testFile = _manager.CreateFile();
        await testFile.Path.WriteAllTextAsync("Cheese for Everyone!");

        await Assert.That(await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None))
            .IsEqualTo(Hash.FromBase64("eSIyd+KOG3s="));
        await Assert.That(await _cache.TryGetHashCache(testFile.Path) != default).IsTrue();

        _cache.Purge(testFile.Path);
        var hash = await testFile.Path.Hash(CancellationToken.None);
        await Assert.That(hash).IsNotEqualTo(default);
        await Assert.That(await _cache.TryGetHashCache(testFile.Path)).IsNotEqualTo(hash);
        await Assert.That(await _cache.FileHashCachedAsync(testFile.Path, CancellationToken.None)).IsEqualTo(hash);

        await Assert.That(await _cache.TryGetHashCache(testFile.Path)).IsEqualTo(hash);

        _cache.VacuumDatabase();
    }
}