using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using Wabbajack.Common;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.GitHub;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class ChunkBuilder
{
    private BA2Chunk _chunk;
    private Stream _dataSlab;
    private long _offsetOffset;
    private uint _packSize;

    public static async Task<ChunkBuilder> Create(BA2DX10File state, BA2Chunk chunk, Stream src,
        DiskSlabAllocator slab, bool useLz4Compression, CancellationToken token)
    {
        var builder = new ChunkBuilder {_chunk = chunk};

        if (!chunk.Compressed)
        {
            builder._dataSlab = slab.Allocate(chunk.FullSz);
            await src.CopyToLimitAsync(builder._dataSlab, (int) chunk.FullSz, token);
        }
        else
        {
            if (!useLz4Compression)
            {
                var deflater = new Deflater(Deflater.BEST_COMPRESSION);
                await using var ms = new MemoryStream();
                await using (var ds = new DeflaterOutputStream(ms, deflater))
                {
                    ds.IsStreamOwner = false;
                    await src.CopyToLimitAsync(ds, (int)chunk.FullSz, token);
                }

                builder._dataSlab = slab.Allocate(ms.Length);
                ms.Position = 0;
                await ms.CopyToLimitAsync(builder._dataSlab, (int)ms.Length, token);
                builder._packSize = (uint)ms.Length;
            }
            else
            {
                byte[] full = new byte[chunk.FullSz];
                await using (var copyStream = new MemoryStream())
                {
                    await src.CopyToLimitAsync(copyStream, (int)chunk.FullSz, token);
                    full = copyStream.ToArray(); 
                }
                var maxOutput = LZ4Codec.MaximumOutputSize((int)chunk.FullSz);
                byte[] compressed = new byte[maxOutput];
                int compressedSize = LZ4Codec.Encode(full, 0, full.Length, compressed, 0, compressed.Length, LZ4Level.L12_MAX);
                var ms = new MemoryStream(compressed, 0, compressedSize);
                builder._dataSlab = slab.Allocate(compressedSize);
                ms.Position = 0;
                await ms.CopyToLimitAsync(builder._dataSlab, compressedSize, token);
                builder._packSize = (uint)compressedSize;
            }
        }

        builder._dataSlab.Position = 0;

        return builder;
    }

    public void WriteHeader(BinaryWriter bw)
    {
        _offsetOffset = bw.BaseStream.Position;
        bw.Write((ulong) 0);
        bw.Write(_packSize);
        bw.Write(_chunk.FullSz);
        bw.Write(_chunk.StartMip);
        bw.Write(_chunk.EndMip);
        bw.Write(_chunk.Align);
    }

    public async ValueTask WriteData(BinaryWriter bw, CancellationToken token)
    {
        var pos = bw.BaseStream.Position;
        bw.BaseStream.Position = _offsetOffset;
        bw.Write((ulong) pos);
        bw.BaseStream.Position = pos;
        await _dataSlab.CopyToLimitAsync(bw.BaseStream, (int) _dataSlab.Length, token);
        await _dataSlab.DisposeAsync();
    }
}