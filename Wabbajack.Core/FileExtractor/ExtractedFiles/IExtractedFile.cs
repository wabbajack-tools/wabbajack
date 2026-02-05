using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.Streams;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.FileExtractor.ExtractedFiles;

public interface IExtractedFile : IStreamFactory
{
    public bool CanMove { get; set; }

    /// <summary>
    ///     Possibly destructive move operation. Should greatly optimize file copies when the file
    ///     exists on the same disk as the newPath. Performs a copy if a move is not possible.
    /// </summary>
    /// <param name="newPath">destination to move the entry to</param>
    /// <returns></returns>
    public ValueTask Move(AbsolutePath newPath, CancellationToken token);


}

public static class IExtractedFileExtensions
{
    public static async Task<Hash> MoveHashedAsync(this IExtractedFile file, AbsolutePath destPath, CancellationToken token)
    {
        if (file.CanMove)
        {
            await file.Move(destPath, token);
            return await destPath.Hash(token);
        }
        else
        {
            await using var s = await file.GetStream();
            return await destPath.WriteAllHashedAsync(s, token, false);        
        }
    }
}