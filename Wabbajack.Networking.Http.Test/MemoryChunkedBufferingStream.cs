using System.IO;
using System.Threading.Tasks;

namespace Wabbajack.Networking.Http.Test;

public class MemoryChunkedBufferingStream : AChunkedBufferingStream
{
    private readonly MemoryStream _src;

    public MemoryChunkedBufferingStream(MemoryStream src) : base(4, src.Length, 16)
    {
        _src = src;
    }

    public override async Task<byte[]> LoadChunk(long offset, int size)
    {
        var buff = new byte[size];
        _src.Position = offset;
        await _src.ReadAsync(buff);
        return buff;
    }
}