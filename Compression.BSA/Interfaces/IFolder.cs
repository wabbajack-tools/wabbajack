using System;
using System.Collections.Generic;
using System.Text;

namespace Compression.BSA
{
    public interface IFolder
    {
        string Name { get; }
        IEnumerable<IFile> Files { get; }
        int FileCount { get; }
    }
}
