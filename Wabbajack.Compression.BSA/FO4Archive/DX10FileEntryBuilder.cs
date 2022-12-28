using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Compression.BSA;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Texture;

namespace Wabbajack.Compression.BSA.FO4Archive;

public class DX10FileEntryBuilder : IFileBuilder
{
    private List<ChunkBuilder> _chunks;
    private BA2DX10File _state;

    public uint FileHash => _state.NameHash;
    public uint DirHash => _state.DirHash;
    public string FullName => (string) _state.Path;
    public int Index => _state.Index;

    public void WriteHeader(BinaryWriter bw, CancellationToken token)
    {
        bw.Write(_state.NameHash);
        bw.Write(Encoding.UTF8.GetBytes(_state.Extension));
        bw.Write(_state.DirHash);
        bw.Write(_state.Unk8);
        bw.Write((byte) _chunks.Count);
        bw.Write(_state.ChunkHdrLen);
        bw.Write(_state.Height);
        bw.Write(_state.Width);
        bw.Write(_state.NumMips);
        bw.Write(_state.PixelFormat);
        bw.Write((byte)_state.IsCubeMap);
        bw.Write((byte)_state.TileMode);

        foreach (var chunk in _chunks)
            chunk.WriteHeader(bw);
    }

    public async ValueTask WriteData(BinaryWriter wtr, CancellationToken token)
    {
        foreach (var chunk in _chunks)
            await chunk.WriteData(wtr, token);
    }

    public static async Task<DX10FileEntryBuilder> Create(BA2DX10File state, Stream src, DiskSlabAllocator slab,
        CancellationToken token)
    {
        var builder = new DX10FileEntryBuilder {_state = state};

        var headerSize = DDS.HeaderSizeForFormat((DXGI_FORMAT) state.PixelFormat) + 4;
        new BinaryReader(src).ReadBytes((int) headerSize);

        // This can't be parallel because it all runs off the same base IO stream.
        builder._chunks = new List<ChunkBuilder>();

        foreach (var chunk in state.Chunks)
            builder._chunks.Add(await ChunkBuilder.Create(state, chunk, src, slab, token));

        return builder;
    }
}