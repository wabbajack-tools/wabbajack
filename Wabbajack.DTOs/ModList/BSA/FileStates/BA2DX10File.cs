using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.FileStates;

[JsonName("BA2DX10Entry")]
[JsonAlias("BA2DX10Entry, Compression.BSA")]
public class BA2DX10File : AFile
{
    public BA2Chunk[] Chunks { get; set; }

    public byte PixelFormat { get; set; }

    public byte NumMips { get; set; }

    public ushort Width { get; set; }

    public ushort Height { get; set; }

    public ushort ChunkHdrLen { get; set; }

    public byte Unk8 { get; set; }

    public uint DirHash { get; set; }

    public string Extension { get; set; }

    public uint NameHash { get; set; }
    public byte IsCubeMap { get; set; }
    public byte TileMode { get; set; }
}