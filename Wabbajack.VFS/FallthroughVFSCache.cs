using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.VFS.Interfaces;

namespace Wabbajack.VFS;

public class FallthroughVFSCache : IVfsCache
{
    private readonly IVfsCache[] _caches;

    public FallthroughVFSCache(IVfsCache[] caches)
    {
        _caches = caches;
    }

    public async Task<IndexedVirtualFile?> Get(Hash hash, CancellationToken token)
    {
        foreach (var cache in _caches)
        {
            var result = await cache.Get(hash, token);
            if (result == null) continue;
            return result;
        }

        return default;
    }

    public async Task Put(IndexedVirtualFile file, CancellationToken token)
    {
        foreach (var cache in _caches)
        {
            await cache.Put(file, token);
        }
    }
}