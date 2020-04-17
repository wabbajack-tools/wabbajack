using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4.Streams;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    public enum VersionType : uint
    {
        TES4 = 0x67,
        FO3 = 0x68, // FO3, FNV, TES5
        SSE = 0x69,
        FO4 = 0x01,
        TES3 = 0xFF // Not a real Bethesda version number
    }

    [Flags]
    public enum ArchiveFlags : uint
    {
        HasFolderNames = 0x1,
        HasFileNames = 0x2,
        Compressed = 0x4,
        Unk4 = 0x8,
        Unk5 = 0x10,
        Unk6 = 0x20,
        XBox360Archive = 0x40,
        Unk8 = 0x80,
        HasFileNameBlobs = 0x100,
        Unk10 = 0x200,
        Unk11 = 0x400
    }

    [Flags]
    public enum FileFlags : uint
    {
        Meshes = 0x1,
        Textures = 0x2,
        Menus = 0x4,
        Sounds = 0x8,
        Voices = 0x10,
        Shaders = 0x20,
        Trees = 0x40,
        Fonts = 0x80,
        Miscellaneous = 0x100
    }

    public class BSAReader : IAsyncDisposable, IBSAReader
    {
        internal uint _archiveFlags;
        internal uint _fileCount;
        internal uint _fileFlags;
        internal AbsolutePath _fileName;
        internal uint _folderCount;
        internal uint _folderRecordOffset;
        private List<FolderRecord> _folders;
        internal string _magic;
        private readonly BinaryReader _rdr;
        private readonly Stream _stream;
        internal uint _totalFileNameLength;
        internal uint _totalFolderNameLength;
        internal uint _version;

        public BSAReader(AbsolutePath filename) : this(filename.OpenRead())
        {
            _fileName = filename;
        }

        public BSAReader(Stream stream)
        {
            _stream = stream;
            _rdr = new BinaryReader(_stream);
            LoadHeaders();
        }

        public IEnumerable<IFile> Files
        {
            get
            {
                foreach (var folder in _folders)
                foreach (var file in folder._files)
                    yield return file;
            }
        }

        public ArchiveStateObject State => new BSAStateObject(this);

        public VersionType HeaderType => (VersionType) _version;

        public ArchiveFlags ArchiveFlags => (ArchiveFlags) _archiveFlags;

        public FileFlags FileFlags => (FileFlags) _archiveFlags;


        public bool HasFolderNames => (_archiveFlags & 0x1) > 0;

        public bool HasFileNames => (_archiveFlags & 0x2) > 0;

        public bool CompressedByDefault => (_archiveFlags & 0x4) > 0;

        public bool Bit9Set => (_archiveFlags & 0x100) > 0;

        public bool HasNameBlobs
        {
            get
            {
                if (HeaderType == VersionType.FO3 || HeaderType == VersionType.SSE) return (_archiveFlags & 0x100) > 0;
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stream.Close();
        }

        private void LoadHeaders()
        {
            var fourcc = Encoding.ASCII.GetString(_rdr.ReadBytes(4));

            if (fourcc != "BSA\0")
                throw new InvalidDataException("Archive is not a BSA");

            _magic = fourcc;
            _version = _rdr.ReadUInt32();
            _folderRecordOffset = _rdr.ReadUInt32();
            _archiveFlags = _rdr.ReadUInt32();
            _folderCount = _rdr.ReadUInt32();
            _fileCount = _rdr.ReadUInt32();
            _totalFolderNameLength = _rdr.ReadUInt32();
            _totalFileNameLength = _rdr.ReadUInt32();
            _fileFlags = _rdr.ReadUInt32();

            LoadFolderRecords();
        }

        private void LoadFolderRecords()
        {
            _folders = new List<FolderRecord>();
            for (var idx = 0; idx < _folderCount; idx += 1)
                _folders.Add(new FolderRecord(this, _rdr));

            foreach (var folder in _folders)
                folder.LoadFileRecordBlock(this, _rdr);

            foreach (var folder in _folders)
            foreach (var file in folder._files)
                file.LoadFileRecord(this, folder, file, _rdr);
        }
    }

    [JsonName("BSAState")]
    public class BSAStateObject : ArchiveStateObject
    {
        public BSAStateObject() { }
        public BSAStateObject(BSAReader bsaReader)
        {
            Magic = bsaReader._magic;
            Version = bsaReader._version;
            ArchiveFlags = bsaReader._archiveFlags;
            FileFlags = bsaReader._fileFlags;

        }

        public override IBSABuilder MakeBuilder(long size)
        {
            return new BSABuilder(this, size);
        }

        public string Magic { get; set; }
        public uint Version { get; set; }
        public uint ArchiveFlags { get; set; }
        public uint FileFlags { get; set; }
    }

    public class FolderRecord
    {
        private readonly uint _fileCount;
        internal List<FileRecord> _files;
        private ulong _offset;
        private uint _unk;

        internal FolderRecord(BSAReader bsa, BinaryReader src)
        {
            Hash = src.ReadUInt64();
            _fileCount = src.ReadUInt32();
            if (bsa.HeaderType == VersionType.SSE)
            {
                _unk = src.ReadUInt32();
                _offset = src.ReadUInt64();
            }
            else
            {
                _offset = src.ReadUInt32();
            }
        }

        public string Name { get; private set; }

        public ulong Hash { get; }

        internal void LoadFileRecordBlock(BSAReader bsa, BinaryReader src)
        {
            if (bsa.HasFolderNames) Name = src.ReadStringLen(bsa.HeaderType);

            _files = new List<FileRecord>();
            for (var idx = 0; idx < _fileCount; idx += 1)
                _files.Add(new FileRecord(bsa, this, src, idx));
        }
    }

    public class FileRecord : IFile
    {
        private readonly BSAReader _bsa;
        private readonly long _dataOffset;
        private readonly uint _dataSize;
        private string _name;
        private readonly string _nameBlob;
        private readonly uint _offset;
        private readonly uint _onDiskSize;
        private readonly uint _originalSize;
        private readonly uint _size;
        internal readonly int _index;

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

            _onDiskSize = (uint) (_size - (_nameBlob == null ? 0 : _nameBlob.Length + 1));

            if (Compressed)
            {
                _dataSize = _originalSize;
                _onDiskSize -= 4;
            }
            else
            {
                _dataSize = _onDiskSize;
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

        public uint Size => _dataSize;
        public FileStateObject State => new BSAFileStateObject(this);

        public ulong Hash { get; }

        public FolderRecord Folder { get; }

        public bool FlipCompression { get; }

        internal void LoadFileRecord(BSAReader bsaReader, FolderRecord folder, FileRecord file, BinaryReader rdr)
        {
            _name = rdr.ReadStringTerm(_bsa.HeaderType);
        }

        public void CopyDataTo(Stream output)
        {
            using (var in_file = _bsa._fileName.OpenRead())
            using (var rdr = new BinaryReader(in_file))
            {
                rdr.BaseStream.Position = _dataOffset;

                if (_bsa.HeaderType == VersionType.SSE)
                {
                    if (Compressed)
                    {
                        var r = LZ4Stream.Decode(rdr.BaseStream);
                        r.CopyToLimit(output, (int) _originalSize);
                    }
                    else
                    {
                        rdr.BaseStream.CopyToLimit(output, (int) _onDiskSize);
                    }
                }
                else
                {
                    if (Compressed)
                        using (var z = new InflaterInputStream(rdr.BaseStream))
                        {
                            z.CopyToLimit(output, (int) _originalSize);
                        }
                    else
                        rdr.BaseStream.CopyToLimit(output, (int) _onDiskSize);
                }
            }
        }

        public byte[] GetData()
        {
            var ms = new MemoryStream();
            CopyDataTo(ms);
            return ms.ToArray();
        }
    }

    [JsonName("BSAFileState")]
    public class BSAFileStateObject : FileStateObject
    {
        public BSAFileStateObject() { }
        public BSAFileStateObject(FileRecord fileRecord)
        {
            FlipCompression = fileRecord.FlipCompression;
            Path = fileRecord.Path;
            Index = fileRecord._index;
        }

        public bool FlipCompression { get; set; }
    }
}
