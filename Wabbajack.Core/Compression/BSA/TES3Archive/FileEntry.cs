using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.TES3Archive;

public class TES3FileEntry : IFile
{
    public uint Offset { get; set; }
    public uint NameOffset { get; set; }
    public uint Hash1 { get; set; }
    public uint Hash2 { get; set; }
    public Reader Archive { get; set; }
    public int Index { get; set; }
    public RelativePath Path { get; set; }
    public uint Size { get; set; }

    public AFile State =>
        new TES3File
        {
            Index = Index,
            Path = Path,
            Size = Size,
            Offset = Offset,
            NameOffset = NameOffset,
            Hash1 = Hash1,
            Hash2 = Hash2
        };

    public async ValueTask CopyDataTo(Stream output, CancellationToken token)
    {
        await using var fs = await Archive._streamFactory.GetStream();
        fs.Position = Archive._dataOffset + Offset;
        await fs.CopyToLimitAsync(output, (int) Size, token);
    }

    public async ValueTask<IStreamFactory> GetStreamFactory(CancellationToken token)
    {
        var ms = new MemoryStream();
        await CopyDataTo(ms, token);
        ms.Position = 0;
        return new MemoryStreamFactory(ms, Path, Archive._streamFactory.LastModifiedUtc);
    }
}