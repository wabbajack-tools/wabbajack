using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.JsonConverters;
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

    public static async Task<T> FromJson<T>(this AbsolutePath path, DTOSerializer? dtos = null)
    {
        await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await fs.FromJson<T>(dtos);
    }
    
    public static async Task<Hash> WriteAllHashedAsync(this AbsolutePath file, Stream srcStream, CancellationToken token,
        bool closeWhenDone = true)
    {
        try
        {
            await using var dest = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            return await srcStream.HashingCopy(dest, token);
        }
        finally
        {
            if (closeWhenDone)
                srcStream.Close();
        }
    }
}