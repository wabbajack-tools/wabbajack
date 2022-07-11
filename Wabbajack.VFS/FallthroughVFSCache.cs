using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Streams;
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

    public async Task<IndexedVirtualFile?> Get(Hash hash, IStreamFactory sf, CancellationToken token)
    {
        IndexedVirtualFile? result = null;
        foreach (var cache in _caches)
        {
            if (result == null)
            {
                result = await cache.Get(hash, sf, token);
                if (result == null) continue;
                foreach (var upperCache in _caches)
                {

                    if (upperCache != cache)
                        await upperCache.Put(result, token);
                }
                return result;
            }
        }

        return result;
    }

    public async Task Put(IndexedVirtualFile file, CancellationToken token)
    {
        foreach (var cache in _caches)
        {
            await cache.Put(file, token);
        }
    }
}