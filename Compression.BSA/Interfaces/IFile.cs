using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Compression.BSA
{
    public interface IFile
    {
        /// <summary>
        /// The path of the file inside the archive
        /// </summary>
        RelativePath Path { get; }

        /// <summary>
        /// The uncompressed file size
        /// </summary>
        uint Size { get;  }

        /// <summary>
        /// Get the metadata for the file.
        /// </summary>
        FileStateObject State { get; }

        /// <summary>
        /// Copies this entry to the given stream. 100% thread safe, the .bsa will be opened multiple times
        /// in order to maintain thread-safe access. 
        /// </summary>
        /// <param name="output"></param>
        ValueTask CopyDataTo(Stream output);

        void Dump(Action<string> print);
        ValueTask<IStreamFactory> GetStreamFactory();
    }
}
