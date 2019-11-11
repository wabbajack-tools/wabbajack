using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Wabbajack.Common.CSP;

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

        public BA2Builder(BA2StateObject state)
        {
            _state = state;
        }
        
        public void Dispose()
        {
        }

        public async Task AddFile(FileStateObject state, Stream src)
        {
            switch (_state.Type)
            {
                case EntryType.GNRL:
                    var result = new BA2FileEntryBuilder((BA2FileEntryState)state, src);
                    lock(_entries) _entries.Add(result);
                    break;
                case EntryType.DX10:
                    var resultdx10 = await BA2DX10FileEntryBuilder.Create((BA2DX10EntryState)state, src);
                    lock(_entries) _entries.Add(resultdx10);
                    break;
            }
        }

        public async Task Build(string filename)
        {
            SortEntries();
            using (var fs = File.OpenWrite(filename))
            using (var bw = new BinaryWriter(fs))
            {
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

                foreach (var entry in _entries)
                {
                    await entry.WriteData(bw);
                }

                if (_state.HasNameTable)
                {
                    var pos = bw.BaseStream.Position;
                    bw.BaseStream.Seek(table_offset_loc, SeekOrigin.Begin);
                    bw.Write((ulong) pos);
                    bw.BaseStream.Seek(pos, SeekOrigin.Begin);

                    foreach (var entry in _entries)
                    {
                        var bytes = Encoding.UTF7.GetBytes(entry.FullName);
                        bw.Write((ushort)bytes.Length);
                        await bw.BaseStream.WriteAsync(bytes, 0, bytes.Length);
                    }
                }
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

        public static async Task<BA2DX10FileEntryBuilder> Create(BA2DX10EntryState state, Stream src)
        {
            var builder = new BA2DX10FileEntryBuilder();
            builder._state = state;

            var header_size = DDS.HeaderSizeForFormat((DXGI_FORMAT) state.PixelFormat) + 4;
            new BinaryReader(src).ReadBytes((int)header_size);

            // TODO: Use ordered pipeline here
            builder._chunks = await state.Chunks.ToChannel()
                                                .Select(async ch => await ChunkBuilder.Create(state, ch, src))
                                                .TakeAll();
            return builder;
        }

        public uint FileHash => _state.NameHash;
        public uint DirHash => _state.DirHash;
        public string FullName => _state.Path;
        public int Index => _state.Index;

        public void WriteHeader(BinaryWriter bw)
        {
            bw.Write(_state.NameHash);
            bw.Write(Encoding.ASCII.GetBytes(_state.Extension));
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
        private byte[] _data;
        private uint _packSize;
        private long _offsetOffset;

        public static async Task<ChunkBuilder> Create(BA2DX10EntryState state, ChunkState chunk, Stream src)
        {
            var builder = new ChunkBuilder {_chunk = chunk};

            using (var ms = new MemoryStream())
            {
                await src.CopyToLimitAsync(ms, (int)chunk.FullSz);
                builder._data = ms.ToArray();
            }

            if (chunk.Compressed)
            {
                using (var ms = new MemoryStream())
                {
                    using (var ds = new DeflaterOutputStream(ms))
                    {
                        ds.Write(builder._data, 0, builder._data.Length);
                    }
                    builder._data = ms.ToArray();
                }
                builder._packSize = (uint)builder._data.Length;
            }

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
            await bw.BaseStream.WriteAsync(_data, 0, _data.Length);
        }
    }

    public class BA2FileEntryBuilder : IFileBuilder
    {
        private byte[] _data;
        private int _rawSize;
        private int _size;
        private BA2FileEntryState _state;
        private long _offsetOffset;

        public BA2FileEntryBuilder(BA2FileEntryState state, Stream src)
        {
            _state = state;
            using (var ms = new MemoryStream())
            {
                src.CopyTo(ms);
                _data = ms.ToArray();
            }
            _rawSize = _data.Length;

            if (state.Compressed)
            {
                using (var ms = new MemoryStream())
                {
                    using (var ds = new DeflaterOutputStream(ms))
                    {
                        ds.Write(_data, 0, _data.Length);
                    }
                    _data = ms.ToArray();
                }

                _size = _data.Length;
            }

        }

        public uint FileHash => _state.NameHash;
        public uint DirHash => _state.DirHash;
        public string FullName => _state.Path;
        public int Index => _state.Index;

        public void WriteHeader(BinaryWriter wtr)
        {
            wtr.Write(_state.NameHash);
            wtr.Write(Encoding.ASCII.GetBytes(_state.Extension));
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
            wtr.BaseStream.Seek(_offsetOffset, SeekOrigin.Begin);
            wtr.Write((ulong)pos);
            wtr.BaseStream.Position = pos;
            await wtr.BaseStream.WriteAsync(_data, 0, _data.Length);
        }
    }
}
