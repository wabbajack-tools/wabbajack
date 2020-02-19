using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Compression.BSA
{
    public class TES3Reader : IBSAReader
    {
        public static string TES3_MAGIC = Encoding.ASCII.GetString(new byte[] {0, 1, 0, 0});
        private uint _versionNumber;
        private uint _hashTableOffset;
        private uint _fileCount;
        private TES3FileEntry[] _files;
        internal long _dataOffset;
        internal string _filename;

        public TES3Reader(string filename)
        {
            _filename = filename;
            using var fs = File.OpenRead(filename);
            using var br = new BinaryReader(fs);
            _versionNumber = br.ReadUInt32();
            _hashTableOffset = br.ReadUInt32();
            _fileCount = br.ReadUInt32();

            _files = new TES3FileEntry[_fileCount];
            for (int i = 0; i < _fileCount; i++)
            {
                var file = new TES3FileEntry {
                    Index = i,
                    Archive = this, 
                    Size = br.ReadUInt32(), 
                    Offset = br.ReadUInt32()
                    
                };
                _files[i] = file;
            }

            for (int i = 0; i < _fileCount; i++)
            {
                _files[i].NameOffset = br.ReadUInt32();
            }

            var origPos = br.BaseStream.Position;
            for (int i = 0; i < _fileCount; i++)
            {
                br.BaseStream.Position = origPos + _files[i].NameOffset;
                _files[i].Path = br.ReadStringTerm(VersionType.TES3);
            }

            br.BaseStream.Position = _hashTableOffset + 12;
            for (int i = 0; i < _fileCount; i++)
            {
                _files[i].Hash1 = br.ReadUInt32();
                _files[i].Hash2 = br.ReadUInt32();
            }

            _dataOffset = br.BaseStream.Position;
        }

        public void Dispose()
        {
        }

        public IEnumerable<IFile> Files => _files;
        public ArchiveStateObject State
        {
            get
            {
                return new TES3ArchiveState
                {
                    FileCount = _fileCount,
                    DataOffset = _dataOffset,
                    HashOffset = _hashTableOffset,
                    VersionNumber = _versionNumber,
                    
                };
            }
        }
    }

    public class TES3ArchiveState : ArchiveStateObject
    {
        public uint FileCount { get; set; }
        public long DataOffset { get; set; }
        public uint HashOffset { get; set; }
        public uint VersionNumber { get; set; }

        public override IBSABuilder MakeBuilder()
        {
            return new TES3Builder(this);
        }
    }

    public class TES3FileEntry : IFile
    {
        public string Path { get; set;  }
        public uint Size { get; set; }
        public FileStateObject State =>
            new TES3FileState
            {
                Index = Index,
                Path = Path,
                Size = Size,
                Offset = Offset,
                NameOffset = NameOffset,
                Hash1 = Hash1,
                Hash2 = Hash2
            };

        public void CopyDataTo(Stream output)
        {
            using var fs = File.OpenRead(Archive._filename);
            fs.Position = Archive._dataOffset + Offset;
            fs.CopyToLimit(output, (int)Size);
        }

        public uint Offset { get; set; }
        public uint NameOffset { get; set; }
        public uint Hash1 { get; set; }
        public uint Hash2 { get; set; }
        public TES3Reader Archive { get; set; }
        public int Index { get; set; }

    }
    
    
    public class TES3FileState : FileStateObject
    {
        public uint Offset { get; set; }
        public uint NameOffset { get; set; }
        public uint Hash1 { get; set; }
        public uint Hash2 { get; set; }
        public uint Size { get; set; }
    }


}
