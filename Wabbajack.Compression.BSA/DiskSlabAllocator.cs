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

    // ReSharper disable once InconsistentNaming
    private const int SHARING_VIOLATION = unchecked((int) 0x80070020);
    // ReSharper disable once InconsistentNaming
    private const int FILE_EXISTS = unchecked((int) 0x80070050);

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

        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            TemporaryPath tempFile = default;
            try
            {
                tempFile = _manager.CreateFile();
                var stream = tempFile.Path.Open(FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                _streams.Add(stream);
                _files.Add(tempFile);
                return stream;
            }
            catch (IOException ex) when (attempt < maxRetries && (ex.HResult == SHARING_VIOLATION
                                         || ex.HResult == FILE_EXISTS
                                         || ex.Message.Contains("being used by another process")))
            {
                Thread.Sleep(1000 * attempt);
            }
        }

        throw new IOException($"Failed to allocate temporary disk slab after {maxRetries} attempts.");
    }
}