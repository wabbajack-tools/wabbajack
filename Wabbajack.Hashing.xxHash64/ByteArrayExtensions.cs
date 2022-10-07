using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.RateLimiter;

namespace Wabbajack.Hashing.xxHash64;

public static class ByteArrayExtensions
{
    public static async ValueTask<Hash> Hash(this byte[] data, IJob? job = null)
    {
        return await new MemoryStream(data).HashingCopy(Stream.Null, CancellationToken.None, job);
    }
}