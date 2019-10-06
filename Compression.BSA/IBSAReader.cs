using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compression.BSA
{
    public interface IBSAReader : IDisposable
    {
        /// <summary>
        /// The files defined by the archive
        /// </summary>
        IEnumerable<IFile> Files { get; }
    }

    public interface IFile
    {
        /// <summary>
        /// The path of the file inside the archive
        /// </summary>
        string Path { get; }

        /// <summary>
        /// The uncompressed file size
        /// </summary>
        uint Size { get;  }

        /// <summary>
        /// Copies this entry to the given stream. 100% thread safe, the .bsa will be opened multiple times
        /// in order to maintain thread-safe access. 
        /// </summary>
        /// <param name="output"></param>
        void CopyDataTo(Stream output);
    }
}
