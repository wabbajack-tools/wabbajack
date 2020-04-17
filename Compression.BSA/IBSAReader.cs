using System;
using System.Collections.Generic;
using System.IO;
using Wabbajack.Common;

namespace Compression.BSA
{
    public interface IBSAReader : IAsyncDisposable
    {
        /// <summary>
        /// The files defined by the archive
        /// </summary>
        IEnumerable<IFile> Files { get; }

        ArchiveStateObject State { get; }
    }

    public interface IBSABuilder : IAsyncDisposable
    {
        void AddFile(FileStateObject state, Stream src);
        void Build(AbsolutePath filename);
    }

    public class ArchiveStateObject
    {
        public virtual IBSABuilder MakeBuilder(long size)
        {
            throw new NotImplementedException();
        }
    }

    public class FileStateObject
    {
        public int Index { get; set; }
        public RelativePath Path { get; set; }
    }
    
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
        void CopyDataTo(Stream output);
    }
}
