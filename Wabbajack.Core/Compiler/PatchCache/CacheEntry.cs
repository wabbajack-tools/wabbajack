using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Compiler.PatchCache;

public record CacheEntry(Hash From, Hash To, long PatchSize, IBinaryPatchCache cache)
{
}