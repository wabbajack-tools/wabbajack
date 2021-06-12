using System.Collections.Generic;

namespace Compression.BSA
{
    public interface IFolder
    {
        string? Name { get; }
        IEnumerable<IFile> Files { get; }
        int FileCount { get; }
    }
}
