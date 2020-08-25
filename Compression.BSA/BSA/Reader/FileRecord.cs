using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
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
        public const int HeaderLength = 0x10;

        private readonly ReadOnlyMemorySlice<byte> _headerData;
        internal readonly int _index;
        internal readonly int _overallIndex;
        internal readonly FileNameBlock _nameBlock;
        internal readonly Lazy<string> _name;
        internal Lazy<(uint Size, uint OnDisk, uint Original)> _size;

        public ulong Hash => BinaryPrimitives.ReadUInt64LittleEndian(_headerData);
        protected uint RawSize => BinaryPrimitives.ReadUInt32LittleEndian(_headerData.Slice(0x8));
        public uint Offset => BinaryPrimitives.ReadUInt32LittleEndian(_headerData.Slice(0xC));
        public string Name => _name.Value;
        public uint Size => _size.Value.Size;

        public bool FlipCompression => (RawSize & (0x1 << 30)) > 0;

        internal FolderRecord Folder { get; }
        internal BSAReader BSA => Folder.BSA;

        internal FileRecord(
            FolderRecord folderRecord, 
            ReadOnlyMemorySlice<byte> data,
            int index,
            int overallIndex,
            FileNameBlock nameBlock)
        {
            _index = index;
            _overallIndex = overallIndex;
            _headerData = data;
            _nameBlock = nameBlock;
            Folder = folderRecord;
            _name = new Lazy<string>(GetName, System.Threading.LazyThreadSafetyMode.PublicationOnly);

            // Will be replaced if CopyDataTo is called before value is created
            _size = new Lazy<(uint Size, uint OnDisk, uint Original)>(
                mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication,
                valueFactory: () =>
                {
                    using var rdr = BSA.GetStream();
                    rdr.BaseStream.Position = Offset;
                    return ReadSize(rdr);
                });
        }

        public RelativePath Path => new RelativePath(string.IsNullOrEmpty(Folder.Name) ? Name : Folder.Name + "\\" + Name, skipValidation: true);

        public bool Compressed
        {
            get
            {
                if (FlipCompression) return !BSA.CompressedByDefault;
                return BSA.CompressedByDefault;
            }
        }

        public FileStateObject State => new BSAFileStateObject(this);

        public async ValueTask CopyDataTo(Stream output)
        {
            await using var in_file = await BSA._fileName.OpenRead().ConfigureAwait(false);
            using var rdr = new BinaryReader(in_file);
            rdr.BaseStream.Position = Offset;

            (uint Size, uint OnDisk, uint Original) size = ReadSize(rdr);
            if (!_size.IsValueCreated)
            {
                _size = new Lazy<(uint Size, uint OnDisk, uint Original)>(value: size);
            }

            if (BSA.HeaderType == VersionType.SSE)
            {
                if (Compressed)
                {
                    using var r = LZ4Stream.Decode(rdr.BaseStream);
                    await r.CopyToLimitAsync(output, size.Original).ConfigureAwait(false);
                }
                else
                {
                    await rdr.BaseStream.CopyToLimitAsync(output, size.OnDisk).ConfigureAwait(false);
                }
            }
            else
            {
                if (Compressed)
                {
                    await using var z = new InflaterInputStream(rdr.BaseStream);
                    await z.CopyToLimitAsync(output, size.Original).ConfigureAwait(false);
                }
                else
                    await rdr.BaseStream.CopyToLimitAsync(output, size.OnDisk).ConfigureAwait(false);
            }
        }

        private string GetName()
        {
            var names = _nameBlock.Names.Value;
            return names[_overallIndex].ReadStringTerm(BSA.HeaderType);
        }

        private (uint Size, uint OnDisk, uint Original) ReadSize(BinaryReader rdr)
        {
            uint size = RawSize;
            if (FlipCompression)
                size = size ^ (0x1 << 30);

            if (Compressed)
                size -= 4;

            byte nameBlobOffset;
            if (BSA.HasNameBlobs)
            {
                nameBlobOffset = rdr.ReadByte();
                // Just skip, not using
                rdr.BaseStream.Position += nameBlobOffset;
                // Minus one more for the size of the name blob offset size
                nameBlobOffset++;
            }
            else
            {
                nameBlobOffset = 0;
            }

            uint originalSize;
            if (Compressed)
            {
                originalSize = rdr.ReadUInt32();
            }
            else
            {
                originalSize = 0;
            }

            uint onDiskSize = size - nameBlobOffset;
            if (Compressed)
            {
                return (Size: originalSize, OnDisk: onDiskSize, Original: originalSize);
            }
            else
            {
                return (Size: onDiskSize, OnDisk: onDiskSize, Original: originalSize);
            }
        }

        public void Dump(Action<string> print)
        {
            print($"Name: {Name}");
            print($"Offset: {Offset}");
            print($"Raw Size: {RawSize}");
            print($"Index: {_index}");
        }
    }
}
