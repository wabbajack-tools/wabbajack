using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Wabbajack.Common
{
    /// <summary>
    /// Memory allocator that stores data via memory mapping to a on-disk file. Disposing of this object
    /// deletes the memory mapped file
    /// </summary>
    public class DiskSlabAllocator : IDisposable
    {
        private readonly TempFile _file;
        private readonly MemoryMappedFile _mmap;
        private long _head = 0;
        private readonly FileStream _fileStream;

        public DiskSlabAllocator(long size)
        {
            _file = new TempFile();
            _fileStream = _file.File.Open(FileMode.Create, FileAccess.ReadWrite);
            _mmap = MemoryMappedFile.CreateFromFile(_fileStream, null, size, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
        }

        public Stream Allocate(long size)
        {
            lock (this)
            {
                var startAt = _head;
                _head += size;
                return _mmap.CreateViewStream(startAt, size, MemoryMappedFileAccess.ReadWrite);
            }
        }

        public void Dispose()
        {
            _mmap.Dispose();
            _fileStream.Dispose();
            _file.Dispose();
        }
    }
}
