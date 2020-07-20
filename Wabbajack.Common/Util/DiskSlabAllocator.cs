using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    /// <summary>
    /// Memory allocator that stores data via memory mapping to a on-disk file. Disposing of this object
    /// deletes the memory mapped file
    /// </summary>
    public class DiskSlabAllocator : IAsyncDisposable
    {
        private TempFile? _file;
        private MemoryMappedFile? _mmap;
        private long _head = 0;
        private FileStream? _fileStream;
        private List<IAsyncDisposable> _allocated = new List<IAsyncDisposable>();
        private long _size;

        private DiskSlabAllocator()
        {

        }

        public static async Task<DiskSlabAllocator> Create(long size)
        {
            var file = new TempFile();
            var fileStream = await file.Path.Create();
            size = Math.Max(size, 1024);
            var self = new DiskSlabAllocator
            {
                _file = file,
                _size = size,
                _fileStream = fileStream,
                _mmap = MemoryMappedFile.CreateFromFile(fileStream, null, size, MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None, false)
            };
            return self;
        }

        public Stream Allocate(long size)
        {
            lock (this)
            {
                // This can happen at times due to differences in compression sizes
                if (_head + size >= _size)
                {
                    return new MemoryStream();
                }

                var startAt = _head;
                _head += size;
                var stream =  _mmap!.CreateViewStream(startAt, size, MemoryMappedFileAccess.ReadWrite);
                _allocated.Add(stream);
                return stream;
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var allocated in _allocated)
                await allocated.DisposeAsync();
            _mmap!.Dispose();
            await _fileStream!.DisposeAsync();
            await _file!.DisposeAsync();
        }
    }
}
