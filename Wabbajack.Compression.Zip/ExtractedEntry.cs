namespace Wabbajack.Compression.Zip;

public class ExtractedEntry
{
    public long FileOffset { get; set; }
    public string FileName { get; set; }
    public long CompressedSize { get; set; }
    public uint UncompressedSize { get; set; }
    public short CompressionMethod { get; set; }
}