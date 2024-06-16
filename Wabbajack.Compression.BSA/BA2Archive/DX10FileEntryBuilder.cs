using DirectXTex;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Texture;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class DX10FileEntryBuilder : IFileBuilder
{
    private List<ChunkBuilder> _chunks;
    private BA2DX10File _state;
    private uint _headerSize = 0;

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
    public uint GetHeaderSize(BA2DX10File state)
    {
        if (_headerSize > 0)
            return _headerSize;

        uint size = 0;
        size += (uint)Marshal.SizeOf(DirectXTexUtility.DDSHeader.DDSMagic);
        size += (uint)Marshal.SizeOf<DirectXTexUtility.DDSHeader>();
        var metadata = DirectXTexUtility.GenerateMetadata(state.Width, state.Height, state.NumMips, (DirectXTexUtility.DXGIFormat)state.PixelFormat, state.IsCubeMap == 1);
        var pixelFormat = DirectXTexUtility.GetPixelFormat(metadata);
        var hasDx10Header = DirectXTexUtility.HasDx10Header(pixelFormat);
        if (hasDx10Header)
            size += (uint)Marshal.SizeOf<DirectXTexUtility.DX10Header>();

        return _headerSize = size;
    }

    public static async Task<DX10FileEntryBuilder> Create(BA2DX10File state, Stream src, DiskSlabAllocator slab, bool useLz4Compression,
        CancellationToken token)
    {
        var builder = new DX10FileEntryBuilder {_state = state};

        var headerSize = builder.GetHeaderSize(state);
        new BinaryReader(src).ReadBytes((int) headerSize);

        // This can't be parallel because it all runs off the same base IO stream.
        builder._chunks = new List<ChunkBuilder>();

        foreach (var chunk in state.Chunks)
            builder._chunks.Add(await ChunkBuilder.Create(state, chunk, src, slab, useLz4Compression, token));

        return builder;
    }
}