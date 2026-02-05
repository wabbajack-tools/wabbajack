using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.TES3Archive;

public class Reader : IReader
{
    public static string TES3_MAGIC = Encoding.ASCII.GetString(new byte[] {0, 1, 0, 0});
    internal long _dataOffset;
    private uint _fileCount;
    private TES3FileEntry[] _files;
    private uint _hashTableOffset;
    public IStreamFactory _streamFactory;
    private uint _versionNumber;

    public IEnumerable<IFile> Files => _files;

    public IArchive State =>
        new TES3State
        {
            FileCount = _fileCount,
            DataOffset = _dataOffset,
            HashOffset = _hashTableOffset,
            VersionNumber = _versionNumber
        };

    public static async ValueTask<Reader> Load(IStreamFactory factory)
    {
        await using var fs = await factory.GetStream();
        using var br = new BinaryReader(fs);
        var rdr = new Reader
        {
            _streamFactory = factory,
            _versionNumber = br.ReadUInt32(),
            _hashTableOffset = br.ReadUInt32(),
            _fileCount = br.ReadUInt32()
        };

        rdr._files = new TES3FileEntry[rdr._fileCount];
        for (var i = 0; i < rdr._fileCount; i++)
        {
            var file = new TES3FileEntry
            {
                Index = i,
                Archive = rdr,
                Size = br.ReadUInt32(),
                Offset = br.ReadUInt32()
            };
            rdr._files[i] = file;
        }

        for (var i = 0; i < rdr._fileCount; i++) rdr._files[i].NameOffset = br.ReadUInt32();

        var origPos = br.BaseStream.Position;
        for (var i = 0; i < rdr._fileCount; i++)
        {
            br.BaseStream.Position = origPos + rdr._files[i].NameOffset;
            rdr._files[i].Path = br.ReadStringTerm(VersionType.TES3).ToRelativePath();
        }

        br.BaseStream.Position = rdr._hashTableOffset + 12;
        for (var i = 0; i < rdr._fileCount; i++)
        {
            rdr._files[i].Hash1 = br.ReadUInt32();
            rdr._files[i].Hash2 = br.ReadUInt32();
        }

        rdr._dataOffset = br.BaseStream.Position;
        return rdr;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dump(Action<string> print)
    {
        throw new NotImplementedException();
    }
}