using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Wabbajack.Common;

namespace Wabbajack.Compression.BSA.TES5Archive;

public class FolderRecord
{
    private readonly ReadOnlyMemorySlice<byte> _data;
    internal readonly Reader BSA;
    internal Lazy<FileRecord[]> _files = null!;
    private int _prevFileCount;
    internal FileNameBlock FileNameBlock = null!;

    internal FolderRecord(Reader bsa, ReadOnlyMemorySlice<byte> data, int index)
    {
        BSA = bsa;
        _data = data;
        Index = index;
    }

    internal int Index { get; }
    public string? Name { get; private set; }

    public IEnumerable<FileRecord> Files => _files.Value;

    private bool IsLongform => BSA.HeaderType == VersionType.SSE;

    public ulong Hash => BinaryPrimitives.ReadUInt64LittleEndian(_data);

    public int FileCount => checked((int) BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(0x8)));

    public uint Unknown => IsLongform ? BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(0xC)) : 0;

    public ulong Offset => IsLongform
        ? BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(0x10))
        : BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(0xC));

    public static int HeaderLength(VersionType version)
    {
        return version switch
        {
            VersionType.SSE => 0x18,
            _ => 0x10
        };
    }

    internal void ProcessFileRecordHeadersBlock(BinaryReader rdr, int fileCountTally)
    {
        _prevFileCount = fileCountTally;
        var totalFileLen = checked(FileCount * FileRecord.HeaderLength);

        ReadOnlyMemorySlice<byte> data;
        if (BSA.HasFolderNames)
        {
            var len = rdr.ReadByte();
            data = rdr.ReadBytes(len + totalFileLen);
            Name = data.Slice(0, len).ReadStringTerm(BSA.HeaderType);
            data = data.Slice(len);
        }
        else
        {
            data = rdr.ReadBytes(totalFileLen);
        }

        _files = new Lazy<FileRecord[]>(
            isThreadSafe: true,
            valueFactory: () => ParseFileRecords(data));
    }

    private FileRecord[] ParseFileRecords(ReadOnlyMemorySlice<byte> data)
    {
        var fileCount = FileCount;
        var ret = new FileRecord[fileCount];
        for (var idx = 0; idx < fileCount; idx += 1)
        {
            var fileData = data.Slice(idx * FileRecord.HeaderLength, FileRecord.HeaderLength);
            ret[idx] = new FileRecord(this, fileData, idx, idx + _prevFileCount, FileNameBlock);
        }

        return ret;
    }
}