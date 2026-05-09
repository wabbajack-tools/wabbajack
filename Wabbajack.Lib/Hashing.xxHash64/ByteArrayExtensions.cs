using System.IO.Hashing;
using System.Threading.Tasks;
using Wabbajack.RateLimiter;

namespace Wabbajack.Hashing.xxHash64;

public static class ByteArrayExtensions
{
    public static ValueTask<Hash> Hash(this byte[] data, IJob? job = null)
    {
        return ValueTask.FromResult(new Hash(XxHash64.HashToUInt64(data)));
    }
}
