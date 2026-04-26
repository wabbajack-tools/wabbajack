using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.VFS;

public static class AbsolutePathExtensions
{
    public static async Task<Hash> Hash(this AbsolutePath src, CancellationToken token)
    {
        await using var fs = src.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await fs.HashingCopy(Stream.Null, token);
    }
}