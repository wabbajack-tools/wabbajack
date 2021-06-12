using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Wabbajack.Common;
#nullable disable

namespace Compression.BSA
{
    interface IFileBuilder
    {
        uint FileHash { get; }
        uint DirHash { get; }
        string FullName { get; }

        int Index { get; }

        Task WriteData(BinaryWriter wtr);
        void WriteHeader(BinaryWriter wtr);

    }
    public class BA2Builder : IBSABuilder
    {
        private BA2StateObject _state;
        private List<IFileBuilder> _entries = new List<IFileBuilder>();
        private DiskSlabAllocator _slab;

        public static async Task<BA2Builder> Create(BA2StateObject state, long size)
        {
            var self = new BA2Builder {_state = state, _slab = await DiskSlabAllocator.Create(size)};
            return self;
        }
        
        public async ValueTask DisposeAsync()
        {
            await _slab.DisposeAsync();
        }

        public async Task AddFile(FileStateObject state, Stream src)
        {
            switch (_state.Type)
            {
                case EntryType.GNRL:
                    var result = await BA2FileEntryBuilder.Create((BA2FileEntryState)state, src, _slab);
                    lock(_entries) _entries.Add(result);
                    break;
                case EntryType.DX10:
                    var resultdx10 = await BA2DX10FileEntryBuilder.Create((BA2DX10EntryState)state, src, _slab);
                    lock(_entries) _entries.Add(resultdx10);
                    break;
            }
        }

        public async Task Build(AbsolutePath filename)
        {
            SortEntries();
            await using var fs = await filename.Create();
            await using var bw = new BinaryWriter(fs);
            
            bw.Write(Encoding.ASCII.GetBytes(_state.HeaderMagic));
            bw.Write(_state.Version);
            bw.Write(Encoding.ASCII.GetBytes(Enum.GetName(typeof(EntryType), _state.Type)));
            bw.Write((uint)_entries.Count);
            var table_offset_loc = bw.BaseStream.Position;
            bw.Write((ulong)0);

            foreach (var entry in _entries)
            {
                entry.WriteHeader(bw);
            }

            await _entries.DoProgress("Writing BSA Files", async entry =>
            {
                await entry.WriteData(bw);
            });

            if (!_state.HasNameTable) return;

            var pos = bw.BaseStream.Position;
            bw.BaseStream.Seek(table_offset_loc, SeekOrigin.Begin);
            bw.Write((ulong)pos);
            bw.BaseStream.Seek(pos, SeekOrigin.Begin);

            foreach (var entry in _entries)
            {
                var bytes = Encoding.UTF8.GetBytes(entry.FullName);
                bw.Write((ushort)bytes.Length);
                await bw.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        private void SortEntries()
        {
            _entries = _entries.OrderBy(e => e.Index).ToList();
        }
    }

    public class BA2DX10FileEntryBuilder : IFileBuilder
    {
        private BA2DX10EntryState _state;
        private List<ChunkBuilder> _chunks;

        public static async Task<BA2DX10FileEntryBuilder> Create(BA2DX10EntryState state, Stream src, DiskSlabAllocator slab)
        {
            var builder = new BA2DX10FileEntryBuilder {_state = state};

            var headerSize = DDS.HeaderSizeForFormat((DXGI_FORMAT) state.PixelFormat) + 4;
            new BinaryReader(src).ReadBytes((int)headerSize);

            // This can't be parallel because it all runs off the same base IO stream.
            builder._chunks = new List<ChunkBuilder>();

            foreach (var chunk in state.Chunks)
                builder._chunks.Add(await ChunkBuilder.Create(state, chunk, src, slab));

            return builder;
        }

        public uint FileHash => _state.NameHash;
        public uint DirHash => _state.DirHash;
        public string FullName => (string)_state.Path;
        public int Index => _state.Index;

        public void WriteHeader(BinaryWriter bw)
        {
            bw.Write(_state.NameHash);
            bw.Write(Encoding.UTF8.GetBytes(_state.Extension));
            bw.Write(_state.DirHash);
            bw.Write(_state.Unk8);
            bw.Write((byte)_chunks.Count);
            bw.Write(_state.ChunkHdrLen);
            bw.Write(_state.Height);
            bw.Write(_state.Width);
            bw.Write(_state.NumMips);
            bw.Write(_state.PixelFormat);
            bw.Write(_state.Unk16);
            
            foreach (var chunk in _chunks)
                chunk.WriteHeader(bw);
        }

        public async Task WriteData(BinaryWriter wtr)
        {
            foreach (var chunk in _chunks)
                await chunk.WriteData(wtr);
        }

    }

    public class ChunkBuilder
    {
        private ChunkState _chunk;
        private uint _packSize;
        private long _offsetOffset;
        private Stream _dataSlab;

        public static async Task<ChunkBuilder> Create(BA2DX10EntryState state, ChunkState chunk, Stream src, DiskSlabAllocator slab)
        {
            var builder = new ChunkBuilder {_chunk = chunk};

            if (!chunk.Compressed)
            { 
                builder._dataSlab = slab.Allocate(chunk.FullSz);
                await src.CopyToWithStatusAsync((int)chunk.FullSz, builder._dataSlab, $"Writing {state.Path} {chunk.StartMip}:{chunk.EndMip}");
            }
            else
            {
                var deflater = new Deflater(Deflater.BEST_COMPRESSION);
                await using var ms = new MemoryStream();
                await using (var ds = new DeflaterOutputStream(ms, deflater))
                {
                    ds.IsStreamOwner = false;
                    await src.CopyToWithStatusAsync((int)chunk.FullSz, ds, $"Compressing {state.Path} {chunk.StartMip}:{chunk.EndMip}");
                }

                builder._dataSlab = slab.Allocate(ms.Length);
                ms.Position = 0;
                await ms.CopyToWithStatusAsync(ms.Length, builder._dataSlab, $"Writing {state.Path} {chunk.StartMip}:{chunk.EndMip}");
                builder._packSize = (uint)ms.Length;
            }
            builder._dataSlab.Position = 0;

            return builder;
        }

        public void WriteHeader(BinaryWriter bw)
        {
            _offsetOffset = bw.BaseStream.Position;
            bw.Write((ulong)0);
            bw.Write(_packSize);
            bw.Write(_chunk.FullSz);
            bw.Write(_chunk.StartMip);
            bw.Write(_chunk.EndMip);
            bw.Write(_chunk.Align);

        }

        public async Task WriteData(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            bw.BaseStream.Position = _offsetOffset;
            bw.Write((ulong)pos);
            bw.BaseStream.Position = pos;
            await _dataSlab.CopyToLimitAsync(bw.BaseStream, (int)_dataSlab.Length);
            await _dataSlab.DisposeAsync();
        }
    }

    public class BA2FileEntryBuilder : IFileBuilder
    {
        private int _rawSize;
        private int _size;
        private BA2FileEntryState _state;
        private long _offsetOffset;
        private Stream _dataSrc;

        public static async Task<BA2FileEntryBuilder> Create(BA2FileEntryState state, Stream src, DiskSlabAllocator slab)
        {
            var builder = new BA2FileEntryBuilder
            {
                _state = state, 
                _rawSize = (int)src.Length,
                _dataSrc = src
            };
            if (!state.Compressed)
                return builder;

            await using (var ms = new MemoryStream())
            {
                await using (var ds = new DeflaterOutputStream(ms))
                {
                    ds.IsStreamOwner = false;
                    await builder._dataSrc.CopyToAsync(ds);
                }

                await builder._dataSrc.DisposeAsync();
                builder._dataSrc = slab.Allocate(ms.Length);
                ms.Position = 0;
                await ms.CopyToAsync(builder._dataSrc);
                builder._dataSrc.Position = 0;
                builder._size = (int)ms.Length;
            }
            return builder;
        }

        public uint FileHash => _state.NameHash;
        public uint DirHash => _state.DirHash;
        public string FullName => (string)_state.Path;
        public int Index => _state.Index;

        public void WriteHeader(BinaryWriter wtr)
        {
            wtr.Write(_state.NameHash);
            wtr.Write(Encoding.UTF8.GetBytes(_state.Extension));
            wtr.Write(_state.DirHash);
            wtr.Write(_state.Flags);
            _offsetOffset = wtr.BaseStream.Position;
            wtr.Write((ulong)0);
            wtr.Write(_size);
            wtr.Write(_rawSize);
            wtr.Write(_state.Align);
        }

        public async Task WriteData(BinaryWriter wtr)
        {
            var pos = wtr.BaseStream.Position;
            wtr.BaseStream.Position = _offsetOffset;
            wtr.Write((ulong)pos);
            wtr.BaseStream.Position = pos;
            _dataSrc.Position = 0;
            await _dataSrc.CopyToLimitAsync(wtr.BaseStream, (int)_dataSrc.Length);
            await _dataSrc.DisposeAsync();
        }
    }
}
