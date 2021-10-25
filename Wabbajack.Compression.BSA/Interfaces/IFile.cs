using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.Interfaces;

public interface IFile
{
    /// <summary>
    ///     The path of the file inside the archive
    /// </summary>
    RelativePath Path { get; }

    /// <summary>
    ///     The uncompressed file size
    /// </summary>
    uint Size { get; }

    /// <summary>
    ///     Get the metadata for the file.
    /// </summary>
    AFile State { get; }

    /// <summary>
    ///     Copies this entry to the given stream. 100% thread safe, the .bsa will be opened multiple times
    ///     in order to maintain thread-safe access.
    /// </summary>
    /// <param name="output"></param>
    ValueTask CopyDataTo(Stream output, CancellationToken token);

    /// <summary>
    ///     Stream factory for this file
    /// </summary>
    /// <returns></returns>
    ValueTask<IStreamFactory> GetStreamFactory(CancellationToken token);
}