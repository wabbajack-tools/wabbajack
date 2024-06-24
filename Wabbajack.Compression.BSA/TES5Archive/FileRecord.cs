using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4.Streams;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.TES5Archive;

public class FileRecord : IFile
{
    public const int HeaderLength = 0x10;

    private readonly ReadOnlyMemorySlice<byte> _headerData;
    internal readonly int _index;
    internal readonly Lazy<string> _name;
    internal readonly FileNameBlock _nameBlock;
    internal readonly int _overallIndex;
    internal Lazy<(uint Size, uint OnDisk, uint Original)> _size;

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
        _name = new Lazy<string>(GetName, LazyThreadSafetyMode.PublicationOnly);

        // Will be replaced if CopyDataTo is called before value is created
        _size = new Lazy<(uint Size, uint OnDisk, uint Original)>(
            mode: LazyThreadSafetyMode.ExecutionAndPublication,
            valueFactory: () =>
            {
                using var rdr = BSA.GetStream();
                rdr.BaseStream.Position = Offset;
                return ReadSize(rdr);
            });
    }

    public ulong Hash => BinaryPrimitives.ReadUInt64LittleEndian(_headerData);
    protected uint RawSize => BinaryPrimitives.ReadUInt32LittleEndian(_headerData.Slice(0x8));
    public uint Offset => BinaryPrimitives.ReadUInt32LittleEndian(_headerData.Slice(0xC));
    public string Name => _name.Value;

    public bool FlipCompression => (RawSize & (0x1 << 30)) > 0;

    internal FolderRecord Folder { get; }
    internal Reader BSA => Folder.BSA;

    public bool Compressed
    {
        get
        {
            if (FlipCompression) return !BSA.CompressedByDefault;
            return BSA.CompressedByDefault;
        }
    }

    public uint Size => _size.Value.Size;

    public RelativePath Path =>
        (string.IsNullOrEmpty(Folder.Name) ? Name : Folder.Name + "\\" + Name).ToRelativePath();

    public AFile State => new BSAFile
    {
        FlipCompression = FlipCompression,
        Index = _index,
        Path = Path
    };

    public async ValueTask CopyDataTo(Stream output, CancellationToken token)
    {
        await using var in_file = await BSA._streamFactory.GetStream().ConfigureAwait(false);
        using var rdr = new BinaryReader(in_file);
        rdr.BaseStream.Position = Offset;

        var size = ReadSize(rdr);
        if (!_size.IsValueCreated) _size = new Lazy<(uint Size, uint OnDisk, uint Original)>(size);

        if (BSA.HeaderType == VersionType.SSE)
        {
            if (Compressed)
            {
                await using var r = LZ4Stream.Decode(rdr.BaseStream);
                await r.CopyToLimitAsync(output, (int) size.Original, token).ConfigureAwait(false);
            }
            else
            {
                await rdr.BaseStream.CopyToLimitAsync(output, (int) size.OnDisk, token).ConfigureAwait(false);
            }
        }
        else
        {
            if (Compressed)
            {
                await using var z = new InflaterInputStream(rdr.BaseStream);
                await z.CopyToLimitAsync(output, (int) size.Original, token).ConfigureAwait(false);
            }
            else
            {
                await rdr.BaseStream.CopyToLimitAsync(output, (int) size.OnDisk, token).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask<IStreamFactory> GetStreamFactory(CancellationToken token)
    {
        var ms = new MemoryStream();
        await CopyDataTo(ms, token);
        ms.Position = 0;
        return new MemoryStreamFactory(ms, Path, BSA._streamFactory.LastModifiedUtc);
    }

    private string GetName()
    {
        var names = _nameBlock.Names.Value;
        return names[_overallIndex].ReadStringTerm(BSA.HeaderType);
    }

    private (uint Size, uint OnDisk, uint Original) ReadSize(BinaryReader rdr)
    {
        var size = RawSize;
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
            originalSize = rdr.ReadUInt32();
        else
            originalSize = 0;

        var onDiskSize = size - nameBlobOffset;
        if (Compressed)
            return (Size: originalSize, OnDisk: onDiskSize, Original: originalSize);
        return (Size: onDiskSize, OnDisk: onDiskSize, Original: originalSize);
    }

    public void Dump(Action<string> print)
    {
        print($"Name: {Name}");
        print($"Offset: {Offset}");
        print($"Raw Size: {RawSize}");
        print($"Index: {_index}");
    }
}