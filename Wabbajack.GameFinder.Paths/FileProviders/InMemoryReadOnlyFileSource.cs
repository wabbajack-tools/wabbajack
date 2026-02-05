using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.GameFinder.Paths.FileProviders;

/// <summary>
/// Read-only file source backed by in-memory byte arrays. Useful for tests.
/// </summary>
public sealed class InMemoryReadOnlyFileSource : IReadOnlyFileSource
{
    private readonly Dictionary<RelativePath, byte[]> _files;

    public InMemoryReadOnlyFileSource(AbsolutePath mountPoint, Dictionary<RelativePath, byte[]> files)
    {
        MountPoint = mountPoint;
        _files = files;
    }

    public AbsolutePath MountPoint { get; }

    public IEnumerable<RelativePath> EnumerateFiles() => _files.Keys;

    
    public Stream OpenRead(RelativePath relativePath)
    {
        if (!_files.TryGetValue(relativePath, out var data))
            throw new FileNotFoundException($"File not found: {relativePath}");
        return new ChunkedStream<IChunkedStreamSource>(new InMemoryChunkedSource(data, 4096));
    }

    private sealed class InMemoryChunkedSource : IChunkedStreamSource
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        public InMemoryChunkedSource(byte[] data, int chunkSize)
        {
            _data = data;
            _chunkSize = Math.Max(1, chunkSize);
        }

        public Size Size => Size.FromLong(_data.LongLength);
        public ulong ChunkCount => (ulong)Math.Ceiling((double)_data.LongLength / _chunkSize);
        public ulong GetOffset(ulong chunkIndex) => (ulong)((long)chunkIndex * _chunkSize);
        public int GetChunkSize(ulong chunkIndex)
        {
            var start = (long)GetOffset(chunkIndex);
            var remaining = Math.Max(0, _data.Length - (int)start);
            return Math.Min(_chunkSize, remaining);
        }
        public Task ReadChunkAsync(Memory<byte> buffer, ulong chunkIndex, CancellationToken token = default)
        {
            ReadChunk(buffer.Span, chunkIndex);
            return Task.CompletedTask;
        }
        public void ReadChunk(Span<byte> buffer, ulong chunkIndex)
        {
            var size = GetChunkSize(chunkIndex);
            _data.AsSpan((int)GetOffset(chunkIndex), size).CopyTo(buffer);
        }
    }
}

