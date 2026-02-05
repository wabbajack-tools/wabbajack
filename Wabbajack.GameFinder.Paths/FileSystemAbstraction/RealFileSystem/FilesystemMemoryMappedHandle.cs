using System;
using System.IO.MemoryMappedFiles;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
///     Represents a 'handle' to a memory mapped file on the Real FileSystem.
/// </summary>
internal class FilesystemMemoryMappedHandle : IDisposable
{
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly MemoryMappedFile _memoryMappedFile;

    /// <summary/>
    public FilesystemMemoryMappedHandle(MemoryMappedViewAccessor accessor, MemoryMappedFile memoryMappedFile)
    {
        _accessor = accessor;
        _memoryMappedFile = memoryMappedFile;
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
    }
}
