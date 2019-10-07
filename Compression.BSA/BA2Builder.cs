using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Compression.BSA
{
    interface IFileBuilder
    {
        uint FileHash { get; }
        uint DirHash { get; }
        string FullName { get; }

        int Index { get; }

        void WriteData(BinaryWriter wtr);
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

        public void AddFile(FileStateObject state, Stream src)
        {
            switch (_state.Type)
            {
                case EntryType.GNRL:
                    var result = new BA2FileEntryBuilder((BA2FileEntryState)state, src);
                    lock(_entries) _entries.Add(result);
                    break;
            }
        }

        public void Build(string filename)
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
                    entry.WriteData(bw);
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
                        bw.Write(bytes);
                    }
                }
            }
        }

        private void SortEntries()
        {
            _entries = _entries.OrderBy(e => e.Index).ToList();
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
                using (var ds = new DeflaterOutputStream(ms))
                {
                    ds.Write(_data, 0, _data.Length);
                }
                _size = _data.Length;
            }

        }

        public uint FileHash => _state.NameHash;
        public uint DirHash => _state.DirHash;
        public string FullName => _state.FullPath;
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

        public void WriteData(BinaryWriter wtr)
        {
            var pos = wtr.BaseStream.Position;
            wtr.BaseStream.Seek(_offsetOffset, SeekOrigin.Begin);
            wtr.Write((ulong)pos);
            wtr.BaseStream.Position = pos;
            wtr.Write(_data);
        }
    }
}
