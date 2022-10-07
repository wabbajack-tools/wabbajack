using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Networking.Http;

public abstract class AChunkedBufferingStream : Stream
{
    private readonly int _chunkSize;
    private readonly int _maxChunks;
    private readonly Dictionary<ulong, byte[]> _chunks;
    private readonly uint _chunkBitsShift;
    private readonly ulong _chunkMask;

    protected AChunkedBufferingStream(int chunkBitsShift, long totalSize, int maxChunks)
    {
        _chunkSize = 1 << chunkBitsShift;
        _chunkBitsShift = (uint)chunkBitsShift;
        _chunkMask = ulong.MaxValue << chunkBitsShift;
        Length = totalSize;
        _maxChunks = maxChunks;
        _chunks = new Dictionary<ulong, byte[]>();
    }

    public abstract Task<byte[]> LoadChunk(long offset, int size);
    
    public override void Flush()
    {
        throw new System.NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (Position >= Length) return 0;
        
        var chunkId = (ulong)Position & _chunkMask;
        if (!_chunks.TryGetValue(chunkId, out var chunkData))
        {
            var chunk = await LoadChunk((long)chunkId, _chunkSize);
            _chunks.Add(chunkId, chunk);
        }
        
        var chunkOffset = (ulong)Position ^ _chunkMask;
        var availableRead = (ulong)chunkData!.Length - chunkOffset;
        var toRead = Math.Min((ulong)count, availableRead);

        Array.Copy(chunkData, (uint)chunkOffset, buffer, offset, count);
        return (int)toRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
    {
        if (Position >= Length) return 0;
        
        var chunkId = (ulong)Position & _chunkMask;
        if (!_chunks.TryGetValue(chunkId, out var chunkData))
        {
            chunkData = await LoadChunk((long)chunkId, _chunkSize);
            _chunks.Add(chunkId, chunkData);
        }
        
        var chunkOffset = (ulong)Position & ~_chunkMask;
        var availableRead = (ulong)chunkData!.Length - chunkOffset;
        var toRead = Math.Min((ulong)buffer.Length, availableRead);

        var chunkBuff = new Memory<byte>(chunkData).Slice((int)chunkOffset, (int)toRead);
        
        chunkBuff.CopyTo(buffer);
        Position += (long)toRead;

        return (int)toRead;
    }


    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin: 
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position += offset;
                break;
            case SeekOrigin.End:
                Position = Length - offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }
        return Position;
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length { get; }
    public override long Position { get; set; } = 0;
}