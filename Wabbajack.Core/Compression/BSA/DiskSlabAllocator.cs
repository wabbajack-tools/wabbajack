using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compression.BSA;

public class DiskSlabAllocator
{
    private readonly ConcurrentBag<TemporaryPath> _files = new();
    private readonly TemporaryFileManager _manager;
    private readonly long _maxMemorySize;
    private readonly ConcurrentBag<Stream> _streams = new();
    private long _memorySize;

    public DiskSlabAllocator(TemporaryFileManager manager, long maxMemorySize = 1024 * 1024 * 256)
    {
        _manager = manager;
        _memorySize = 0;
        _maxMemorySize = maxMemorySize;
    }

    public async Task DisposeAsync()
    {
        foreach (var s in _streams)
            await s.DisposeAsync();

        foreach (var file in _files) 
            await file.DisposeAsync();
    }

    public Stream Allocate(long rLength)
    {
        var newSize = Interlocked.Add(ref _memorySize, rLength);
        if (newSize < _maxMemorySize)
        {
            var stream = new MemoryStream();
            _streams.Add(stream);
            return stream;
        }
        else
        {
            var tempFile = _manager.CreateFile();
            var stream = tempFile.Path.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            _streams.Add(stream);
            _files.Add(tempFile);
            return stream;
        }
    }
}