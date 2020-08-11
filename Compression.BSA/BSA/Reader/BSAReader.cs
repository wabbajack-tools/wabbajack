using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    public class BSAReader : IBSAReader
    {
        internal uint _fileCount;
        internal AbsolutePath _fileName;
        internal uint _folderCount;
        internal uint _folderRecordOffset;
        private List<FolderRecord> _folders;
        internal string _magic;
        internal uint _totalFileNameLength;
        internal uint _totalFolderNameLength;

        public VersionType HeaderType { get; private set; }

        public ArchiveFlags ArchiveFlags { get; private set; }

        public FileFlags FileFlags { get; private set; }

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
            using var stream = File.Open(filename.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(stream);
            var bsa = new BSAReader { _fileName = filename };
            bsa.LoadHeaders(br);
            return bsa;
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

            LoadFolderRecords(rdr);
        }

        private void LoadFolderRecords(BinaryReader rdr)
        {
            _folders = new List<FolderRecord>();
            for (var idx = 0; idx < _folderCount; idx += 1)
                _folders.Add(new FolderRecord(this, rdr));

            foreach (var folder in _folders)
                folder.LoadFileRecordBlock(this, rdr);

            foreach (var folder in _folders)
            foreach (var file in folder._files)
                file.LoadFileRecord(this, folder, file, rdr);
        }
    }
}
