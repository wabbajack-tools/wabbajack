using System;
using System.Collections.Generic;

namespace Compression.BSA
{
    public interface IBSAReader
    {
        /// <summary>
        /// The files defined by the archive
        /// </summary>
        IEnumerable<IFile> Files { get; }

        ArchiveStateObject State { get; }

        void Dump(Action<string> print);
    }
}
