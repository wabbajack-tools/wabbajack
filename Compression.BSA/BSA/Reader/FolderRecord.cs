using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    public class FolderRecord
    {
        private readonly uint _fileCount;
        internal List<FileRecord> _files;
        private ulong _offset;
        private uint _unk;

        public string Name { get; private set; }

        public ulong Hash { get; }

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

        internal void LoadFileRecordBlock(BSAReader bsa, BinaryReader src)
        {
            if (bsa.HasFolderNames) Name = src.ReadStringLen(bsa.HeaderType);

            _files = new List<FileRecord>();
            for (var idx = 0; idx < _fileCount; idx += 1)
                _files.Add(new FileRecord(bsa, this, src, idx));
        }
    }
}
