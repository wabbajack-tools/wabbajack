namespace Wabbajack.DTOs.BSA.FileStates;

public class BA2Chunk
{
    public bool Compressed { get; set; }
    public uint Align { get; set; }
    public ushort EndMip { get; set; }
    public ushort StartMip { get; set; }
    public uint FullSz { get; set; }
}