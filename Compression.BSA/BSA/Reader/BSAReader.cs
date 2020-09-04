using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    public class BSAReader : IBSAReader
    {
        public const int HeaderLength = 0x24;

        internal uint _fileCount;
        internal AbsolutePath _fileName;
        internal uint _folderCount;
        internal uint _folderRecordOffset;
        private Lazy<FolderRecord[]> _folders = null!;
        private Lazy<Dictionary<string, FolderRecord>> _foldersByName = null!;
        internal string _magic = string.Empty;
        internal uint _totalFileNameLength;
        internal uint _totalFolderNameLength;

        public VersionType HeaderType { get; private set; }

        public ArchiveFlags ArchiveFlags { get; private set; }

        public FileFlags FileFlags { get; private set; }

        public IEnumerable<IFile> Files => _folders.Value.SelectMany(f => f.Files);

        public IEnumerable<IFolder> Folders => _folders.Value;

        public ArchiveStateObject State => new BSAStateObject(this);

        public bool HasFolderNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFolderNames);

        public bool HasFileNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNames);

        public bool CompressedByDefault => ArchiveFlags.HasFlag(ArchiveFlags.Compressed);

        public bool Bit9Set => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNameBlobs);

        public bool HasNameBlobs
        {
            get
            {
                if (HeaderType == VersionType.FO3 || HeaderType == VersionType.SSE) return Bit9Set;
                return false;
            }
        }

        public void Dump(Action<string> print)
        {
            print($"File Name: {_fileName}");
            print($"File Count: {_fileCount}");
            print($"Magic: {_magic}");

            foreach (var file in Files)
            {
                print("\n");
                file.Dump(print);
            }
        }

        public static async ValueTask<BSAReader> LoadAsync(AbsolutePath filename)
        {
            using var stream = await filename.OpenRead().ConfigureAwait(false);
            using var br = new BinaryReader(stream);
            var bsa = new BSAReader { _fileName = filename };
            bsa.LoadHeaders(br);
            return bsa;
        }


        public static BSAReader Load(AbsolutePath filename)
        {
            var bsa = new BSAReader { _fileName = filename };
            using var rdr = bsa.GetStream();
            bsa.LoadHeaders(rdr);
            return bsa;
        }

        internal BinaryReader GetStream()
        {
            return new BinaryReader(File.Open(_fileName.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        private void LoadHeaders(BinaryReader rdr)
        {
            var fourcc = Encoding.ASCII.GetString(rdr.ReadBytes(4));

            if (fourcc != "BSA\0")
                throw new InvalidDataException("Archive is not a BSA");

            _magic = fourcc;
            HeaderType = (VersionType)rdr.ReadUInt32();
            _folderRecordOffset = rdr.ReadUInt32();
            ArchiveFlags = (ArchiveFlags)rdr.ReadUInt32();
            _folderCount = rdr.ReadUInt32();
            _fileCount = rdr.ReadUInt32();
            _totalFolderNameLength = rdr.ReadUInt32();
            _totalFileNameLength = rdr.ReadUInt32();
            FileFlags = (FileFlags)rdr.ReadUInt32();

            _folders = new Lazy<FolderRecord[]>(
                isThreadSafe: true,
                valueFactory: () => LoadFolderRecords());
            _foldersByName = new Lazy<Dictionary<string, FolderRecord>>(
                isThreadSafe: true,
                valueFactory: GetFolderDictionary);
        }

        private FolderRecord[] LoadFolderRecords()
        {
            using var rdr = GetStream();
            rdr.BaseStream.Position = _folderRecordOffset;
            var folderHeaderLength = FolderRecord.HeaderLength(HeaderType);
            ReadOnlyMemorySlice<byte> folderHeaderData = rdr.ReadBytes(checked((int)(folderHeaderLength * _folderCount)));

            var ret = new FolderRecord[_folderCount];
            for (var idx = 0; idx < _folderCount; idx += 1)
                ret[idx] = new FolderRecord(this, folderHeaderData.Slice(idx * folderHeaderLength, folderHeaderLength), idx);

            // Slice off appropriate file header data per folder
            int fileCountTally = 0;
            foreach (var folder in ret)
            {
                folder.ProcessFileRecordHeadersBlock(rdr, fileCountTally);
                fileCountTally = checked((int)(fileCountTally + folder.FileCount));
            }

            if (HasFileNames)
            {
                var filenameBlock = new FileNameBlock(this, rdr.BaseStream.Position);
                foreach (var folder in ret)
                {
                    folder.FileNameBlock = filenameBlock;
                }
            }

            return ret;
        }

        private Dictionary<string, FolderRecord> GetFolderDictionary()
        {
            if (!HasFolderNames)
            {
                throw new ArgumentException("Cannot get folders by name if the BSA does not have folder names.");
            }
            var ret = new Dictionary<string, FolderRecord>();
            foreach (var folder in _folders.Value)
            {
                ret.Add(folder.Name!, folder);
            }
            return ret;
        }

        public bool TryGetFolder(string path, [MaybeNullWhen(false)] out IFolder folder)
        {
            if (!HasFolderNames
                || !_foldersByName.Value.TryGetValue(path, out var folderRec))
            {
                folder = default;
                return false;
            }
            folder = folderRec;
            return true;
        }
    }
}
