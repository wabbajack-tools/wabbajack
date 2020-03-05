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
        private TempFile _file;
        private MemoryMappedFile _mmap;
        private long _head = 0;
        private string _name;

        public DiskSlabAllocator()
        {
            _name = Guid.NewGuid().ToString();
            _mmap = MemoryMappedFile.CreateNew(null, (long)1 << 34);
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
            _mmap?.Dispose();
        }
    }
}
