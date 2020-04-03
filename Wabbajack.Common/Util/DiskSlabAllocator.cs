using System;
using System.Collections.Generic;
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
        private List<IDisposable> _allocated = new List<IDisposable>();
        private long _size;

        public DiskSlabAllocator(long size)
        {
            _file = new TempFile();
            _fileStream = _file.File.Open(FileMode.Create, FileAccess.ReadWrite);
            _size = size;
            _mmap = MemoryMappedFile.CreateFromFile(_fileStream, null, size, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
        }

        public Stream Allocate(long size)
        {
            lock (this)
            {
                if (_head + size >= _size)
                    throw new InvalidDataException($"Size out of range. Declared {_size} used {_head + size}");
                var startAt = _head;
                _head += size;
                var stream =  _mmap.CreateViewStream(startAt, size, MemoryMappedFileAccess.ReadWrite);
                _allocated.Add(stream);
                return stream;
            }
        }

        public void Dispose()
        {
            _allocated.Do(s => s.Dispose());
            _mmap.Dispose();
            _fileStream.Dispose();
            _file.Dispose();
        }
    }
}
