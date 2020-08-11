using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4.Streams;
using Wabbajack.Common;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    public class FileRecord : IFile
    {
        private readonly BSAReader _bsa;
        private readonly long _dataOffset;
        private string _name;
        private readonly string _nameBlob;
        private readonly uint _offset;
        private readonly uint _onDiskSize;
        private readonly uint _originalSize;
        private readonly uint _size;
        internal readonly int _index;

        public uint Size { get; }

        public ulong Hash { get; }

        public FolderRecord Folder { get; }

        public bool FlipCompression { get; }

        public FileRecord(BSAReader bsa, FolderRecord folderRecord, BinaryReader src, int index)
        {
            _index = index;
            _bsa = bsa;
            Hash = src.ReadUInt64();
            var size = src.ReadUInt32();
            FlipCompression = (size & (0x1 << 30)) > 0;

            if (FlipCompression)
                _size = size ^ (0x1 << 30);
            else
                _size = size;

            if (Compressed)
                _size -= 4;

            _offset = src.ReadUInt32();
            Folder = folderRecord;

            var old_pos = src.BaseStream.Position;

            src.BaseStream.Position = _offset;

            if (bsa.HasNameBlobs)
                _nameBlob = src.ReadStringLenNoTerm(bsa.HeaderType);


            if (Compressed)
                _originalSize = src.ReadUInt32();

            _onDiskSize = (uint)(_size - (_nameBlob == null ? 0 : _nameBlob.Length + 1));

            if (Compressed)
            {
                Size = _originalSize;
                _onDiskSize -= 4;
            }
            else
            {
                Size = _onDiskSize;
            }

            _dataOffset = src.BaseStream.Position;

            src.BaseStream.Position = old_pos;
        }

        public RelativePath Path
        {
            get
            {
                return string.IsNullOrEmpty(Folder.Name) ? new RelativePath(_name) : new RelativePath(Folder.Name + "\\" + _name);
            }
        }

        public bool Compressed
        {
            get
            {
                if (FlipCompression) return !_bsa.CompressedByDefault;
                return _bsa.CompressedByDefault;
            }
        }

        public FileStateObject State => new BSAFileStateObject(this);

        internal void LoadFileRecord(BSAReader bsaReader, FolderRecord folder, FileRecord file, BinaryReader rdr)
        {
            _name = rdr.ReadStringTerm(_bsa.HeaderType);
        }

        public async ValueTask CopyDataTo(Stream output)
        {
            await using var in_file = await _bsa._fileName.OpenRead().ConfigureAwait(false);
            using var rdr = new BinaryReader(in_file);
            rdr.BaseStream.Position = _dataOffset;

            if (_bsa.HeaderType == VersionType.SSE)
            {
                if (Compressed)
                {
                    using var r = LZ4Stream.Decode(rdr.BaseStream);
                    await r.CopyToLimitAsync(output, (int)_originalSize).ConfigureAwait(false);
                }
                else
                {
                    await rdr.BaseStream.CopyToLimitAsync(output, (int)_onDiskSize).ConfigureAwait(false);
                }
            }
            else
            {
                if (Compressed)
                {
                    await using var z = new InflaterInputStream(rdr.BaseStream);
                    await z.CopyToLimitAsync(output, (int)_originalSize).ConfigureAwait(false);
                }
                else
                    await rdr.BaseStream.CopyToLimitAsync(output, (int)_onDiskSize).ConfigureAwait(false);
            }
        }

        public void Dump(Action<string> print)
        {
            print($"Name: {_name}");
            print($"Offset: {_offset}");
            print($"On Disk Size: {_onDiskSize}");
            print($"Original Size: {_originalSize}");
            print($"Size: {_size}");
            print($"Index: {_index}");
        }
    }
}
