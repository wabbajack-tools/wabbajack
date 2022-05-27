using System.IO;
using System.Threading.Tasks;
using Wabbajack.Compiler.PatchCache;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.RateLimiter;

namespace Wabbajack.Compiler;

public interface IBinaryPatchCache
{
    public Task<CacheEntry> CreatePatch(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash, IJob? job = null);

    public Task<CacheEntry?> GetPatch(Hash hashA, Hash hashB);
    public Task<byte[]> GetData(CacheEntry entry);
}