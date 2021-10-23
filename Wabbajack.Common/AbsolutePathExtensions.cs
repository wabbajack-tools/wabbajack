using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Common;

public static class AbsolutePathExtensions
{
    public static async Task<Hash> Hash(this AbsolutePath path, CancellationToken? token = null)
    {
        await using var fs = path.Open(FileMode.Open);
        return await fs.HashingCopy(Stream.Null, token ?? CancellationToken.None);
    }
}