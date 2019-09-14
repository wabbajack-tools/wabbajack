using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Compression.BSA
{
    public class BSABuilder : IDisposable
    {
        internal uint _archiveFlags;
        internal uint _fileCount;
        internal uint _fileFlags;
        internal byte[] _fileId;

        private List<FileEntry> _files = new List<FileEntry>();
        internal uint _folderCount;
        internal List<FolderRecordBuilder> _folders = new List<FolderRecordBuilder>();
        internal uint _offset;
        internal uint _totalFileNameLength;
        internal uint _totalFolderNameLength;
        internal uint _version;

        public BSABuilder()
        {
            _fileId = Encoding.ASCII.GetBytes("BSA\0");
            _offset = 0x24;
        }

        public IEnumerable<FileEntry> Files => _files;

        public ArchiveFlags ArchiveFlags
        {
            get => (ArchiveFlags) _archiveFlags;
            set => _archiveFlags = (uint) value;
        }

        public FileFlags FileFlags
        {
            get => (FileFlags) _archiveFlags;
            set => _archiveFlags = (uint) value;
        }

        public VersionType HeaderType
        {
            get => (VersionType) _version;
            set => _version = (uint) value;
        }

        public IEnumerable<string> FolderNames
        {
            get
            {
                return _files.Select(f => Path.GetDirectoryName(f.Path))
                    .ToHashSet();
            }
        }

        public bool HasFolderNames => (_archiveFlags & 0x1) > 0;

        public bool HasFileNames => (_archiveFlags & 0x2) > 0;

        public bool CompressedByDefault => (_archiveFlags & 0x4) > 0;

        public bool HasNameBlobs => (_archiveFlags & 0x100) > 0;

        public void Dispose()
        {
        }

        public FileEntry AddFile(string path, Stream src, bool flipCompression = false)
        {
            var r = new FileEntry(this, path, src, flipCompression);

            lock (this)
            {
                _files.Add(r);
            }

            return r;
        }

        public void Build(string outputName)
        {
            RegenFolderRecords();
            if (File.Exists(outputName)) File.Delete(outputName);

            using (var fs = File.OpenWrite(outputName))
            using (var wtr = new BinaryWriter(fs))
            {
                wtr.Write(_fileId);
                wtr.Write(_version);
                wtr.Write(_offset);
                wtr.Write(_archiveFlags);
                var folders = FolderNames.ToList();
                wtr.Write((uint) folders.Count);
                wtr.Write((uint) _files.Count);
                wtr.Write((uint) _folders.Select(f => f._nameBytes.Count() - 1).Sum()); // totalFolderNameLength
                var s = _files.Select(f => f._pathBytes.Count()).Sum();
                _totalFileNameLength = (uint) _files.Select(f => f._nameBytes.Count()).Sum();
                wtr.Write(_totalFileNameLength); // totalFileNameLength
                wtr.Write(_fileFlags);

                foreach (var folder in _folders) folder.WriteFolderRecord(wtr);

                foreach (var folder in _folders)
                {
                    if (HasFolderNames)
                        wtr.Write(folder._nameBytes);
                    foreach (var file in folder._files) file.WriteFileRecord(wtr);
                }

                foreach (var file in _files) wtr.Write(file._nameBytes);

                foreach (var file in _files) file.WriteData(wtr);
            }
        }

        public void RegenFolderRecords()
        {
            _folders = _files.GroupBy(f => Path.GetDirectoryName(f.Path.ToLowerInvariant()))
                .Select(f => new FolderRecordBuilder(this, f.Key, f.ToList()))
                .OrderBy(f => f._hash)
                .ToList();

            var lnk = _files.Where(f => f.Path.EndsWith(".lnk")).FirstOrDefault();

            foreach (var folder in _folders)
            foreach (var file in folder._files)
                file._folder = folder;

            _files = (from folder in _folders
                from file in folder._files
                orderby folder._hash, file._hash
                select file).ToList();
        }
    }

    public class FolderRecordBuilder
    {
        internal BSABuilder _bsa;
        internal uint _fileCount;
        internal IEnumerable<FileEntry> _files;
        internal ulong _hash;
        internal byte[] _nameBytes;
        internal ulong _offset;
        internal uint _recordSize;

        public FolderRecordBuilder(BSABuilder bsa, string folderName, IEnumerable<FileEntry> files)
        {
            _files = files.OrderBy(f => f._hash);
            Name = folderName.ToLowerInvariant();
            _bsa = bsa;
            // Folders don't have extensions, so let's make sure we cut it out
            _hash = Name.GetBSAHash("");
            _fileCount = (uint) files.Count();
            _nameBytes = folderName.ToBZString(_bsa.HeaderType);
            _recordSize = sizeof(ulong) + sizeof(uint) + sizeof(uint);
        }

        public ulong Hash => _hash;

        public string Name { get; }

        public ulong SelfSize
        {
            get
            {
                if (_bsa.HeaderType == VersionType.SSE)
                    return sizeof(ulong) + sizeof(uint) + sizeof(uint) + sizeof(ulong);
                return sizeof(ulong) + sizeof(uint) + sizeof(uint);
            }
        }

        public ulong FileRecordSize
        {
            get
            {
                ulong size = 0;
                if (_bsa.HasFolderNames)
                    size += (ulong) _nameBytes.Length;
                size += (ulong) _files.Select(f => sizeof(ulong) + sizeof(uint) + sizeof(uint)).Sum();
                return size;
            }
        }

        public void WriteFolderRecord(BinaryWriter wtr)
        {
            var idx = _bsa._folders.IndexOf(this);
            _offset = (ulong) wtr.BaseStream.Position;
            _offset += (ulong) _bsa._folders.Skip(idx).Select(f => (long) f.SelfSize).Sum();
            _offset += _bsa._totalFileNameLength;
            _offset += (ulong) _bsa._folders.Take(idx).Select(f => (long) f.FileRecordSize).Sum();

            var sp = wtr.BaseStream.Position;
            wtr.Write(_hash);
            wtr.Write(_fileCount);
            if (_bsa.HeaderType == VersionType.SSE)
            {
                wtr.Write((uint) 0); // unk
                wtr.Write(_offset); // offset
            }
            else if (_bsa.HeaderType == VersionType.FO3 || _bsa.HeaderType == VersionType.TES4)
            {
                wtr.Write((uint) _offset);
            }
            else
            {
                throw new NotImplementedException($"Cannot write to BSAs of type {_bsa.HeaderType}");
            }
        }
    }

    public class FileEntry
    {
        internal BSABuilder _bsa;
        internal Stream _bytesSource;
        internal string _filenameSource;
        internal bool _flipCompression;
        internal FolderRecordBuilder _folder;

        internal ulong _hash;
        internal string _name;
        internal byte[] _nameBytes;
        private long _offsetOffset;
        internal int _originalSize;
        internal string _path;
        private readonly byte[] _pathBSBytes;
        internal byte[] _pathBytes;
        internal byte[] _rawData;

        public FileEntry(BSABuilder bsa, string path, Stream src, bool flipCompression)
        {
            _bsa = bsa;
            _path = path.ToLowerInvariant();
            _name = System.IO.Path.GetFileName(_path);
            _hash = _name.GetBSAHash();
            _nameBytes = _name.ToTermString(bsa.HeaderType);
            _pathBytes = _path.ToTermString(bsa.HeaderType);
            _pathBSBytes = _path.ToBSString();
            _flipCompression = flipCompression;

            var ms = new MemoryStream();
            src.CopyTo(ms);
            _rawData = ms.ToArray();
            _originalSize = _rawData.Length;

            if (Compressed)
                CompressData();
        }

        public bool Compressed
        {
            get
            {
                if (_flipCompression)
                    return !_bsa.CompressedByDefault;
                return _bsa.CompressedByDefault;
            }
        }

        public string Path => _path;

        public bool FlipCompression => _flipCompression;

        public ulong Hash => _hash;

        public FolderRecordBuilder Folder => _folder;

        private void CompressData()
        {
            if (_bsa.HeaderType == VersionType.SSE)
            {
                var r = new MemoryStream();
                using (var w = LZ4Stream.Encode(r, new LZ4EncoderSettings {CompressionLevel = LZ4Level.L10_OPT}))
                {
                    new MemoryStream(_rawData).CopyTo(w);
                }

                _rawData = r.ToArray();
            }
            else if (_bsa.HeaderType == VersionType.FO3 || _bsa.HeaderType == VersionType.TES4)
            {
                var r = new MemoryStream();
                using (var w = new DeflaterOutputStream(r))
                {
                    new MemoryStream(_rawData).CopyTo(w);
                }

                _rawData = r.ToArray();
            }
            else
            {
                throw new NotImplementedException($"Can't compress data for {_bsa.HeaderType} BSAs.");
            }
        }

        internal void WriteFileRecord(BinaryWriter wtr)

        {
            wtr.Write(_hash);
            var size = _rawData.Length;
            if (_bsa.HasNameBlobs) size += _pathBSBytes.Length;
            if (Compressed) size += 4;
            if (_flipCompression)
                wtr.Write((uint) size | (0x1 << 30));
            else
                wtr.Write((uint) size);

            _offsetOffset = wtr.BaseStream.Position;
            wtr.Write(0xDEADBEEF);
        }

        internal void WriteData(BinaryWriter wtr)
        {
            var offset = (uint) wtr.BaseStream.Position;
            wtr.BaseStream.Position = _offsetOffset;
            wtr.Write(offset);
            wtr.BaseStream.Position = offset;

            if (_bsa.HasNameBlobs) wtr.Write(_pathBSBytes);

            if (Compressed)
            {
                wtr.Write((uint) _originalSize);
                wtr.Write(_rawData);
            }
            else
            {
                wtr.Write(_rawData);
            }
        }
    }
}