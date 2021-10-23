using System.IO;

namespace Wabbajack.Compression.BSA.FO4Archive;

public class TextureChunk
{
    internal uint _align;
    internal ushort _endMip;
    internal uint _fullSz;
    internal ulong _offset;
    internal uint _packSz;
    internal ushort _startMip;

    public TextureChunk(BinaryReader rdr)
    {
        _offset = rdr.ReadUInt64();
        _packSz = rdr.ReadUInt32();
        _fullSz = rdr.ReadUInt32();
        _startMip = rdr.ReadUInt16();
        _endMip = rdr.ReadUInt16();
        _align = rdr.ReadUInt32();
    }
}