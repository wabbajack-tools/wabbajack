using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.Compression.BSA.TES3Archive;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.TES5Archive;

public class Reader : IReader
{
    public const int HeaderLength = 0x24;

    internal uint _fileCount;
    internal uint _folderCount;
    internal uint _folderRecordOffset;
    private Lazy<FolderRecord[]> _folders = null!;
    private Lazy<Dictionary<string, FolderRecord>> _foldersByName = null!;
    internal string _magic = string.Empty;
    public IStreamFactory _streamFactory = new NativeFileStreamFactory(default);
    internal uint _totalFileNameLength;
    internal uint _totalFolderNameLength;

    public VersionType HeaderType { get; private set; }

    public ArchiveFlags ArchiveFlags { get; private set; }

    public FileFlags FileFlags { get; private set; }

    public IEnumerable<FolderRecord> Folders => _folders.Value;

    public bool HasFolderNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFolderNames);

    public bool HasFileNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNames);

    public bool CompressedByDefault => ArchiveFlags.HasFlag(ArchiveFlags.Compressed);

    public bool Bit9Set => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNameBlobs);

    public bool HasNameBlobs => HeaderType is VersionType.FO3 or VersionType.SSE && Bit9Set;

    public IEnumerable<IFile> Files => _folders.Value.SelectMany(f => f.Files);

    public IArchive State => new BSAState
    {
        Magic = _magic,
        Version = (uint) HeaderType,
        ArchiveFlags = (uint) ArchiveFlags,
        FileFlags = (uint) FileFlags
    };

    public static async ValueTask<Reader> Load(IStreamFactory factory)
    {
        await using var stream = await factory.GetStream().ConfigureAwait(false);
        using var br = new BinaryReader(stream);
        var bsa = new Reader {_streamFactory = factory};
        bsa.LoadHeaders(br);
        return bsa;
    }


    public static Reader Load(AbsolutePath filename)
    {
        var bsa = new Reader {_streamFactory = new NativeFileStreamFactory(filename)};
        using var rdr = bsa.GetStream();
        bsa.LoadHeaders(rdr);
        return bsa;
    }

    internal BinaryReader GetStream()
    {
        return new BinaryReader(_streamFactory.GetStream().Result);
    }

    private void LoadHeaders(BinaryReader rdr)
    {
        var fourcc = Encoding.ASCII.GetString(rdr.ReadBytes(4));

        if (fourcc != "BSA\0")
            throw new InvalidDataException("Archive is not a BSA");

        _magic = fourcc;
        HeaderType = (VersionType) rdr.ReadUInt32();
        _folderRecordOffset = rdr.ReadUInt32();
        ArchiveFlags = (ArchiveFlags) rdr.ReadUInt32();
        _folderCount = rdr.ReadUInt32();
        _fileCount = rdr.ReadUInt32();
        _totalFolderNameLength = rdr.ReadUInt32();
        _totalFileNameLength = rdr.ReadUInt32();
        FileFlags = (FileFlags) rdr.ReadUInt32();

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
        ReadOnlyMemorySlice<byte> folderHeaderData =
            rdr.ReadBytes(checked((int) (folderHeaderLength * _folderCount)));

        var ret = new FolderRecord[_folderCount];
        for (var idx = 0; idx < _folderCount; idx += 1)
            ret[idx] = new FolderRecord(this, folderHeaderData.Slice(idx * folderHeaderLength, folderHeaderLength),
                idx);

        // Slice off appropriate file header data per folder
        var fileCountTally = 0;
        foreach (var folder in ret)
        {
            folder.ProcessFileRecordHeadersBlock(rdr, fileCountTally);
            fileCountTally = checked(fileCountTally + folder.FileCount);
        }

        if (HasFileNames)
        {
            var filenameBlock = new FileNameBlock(this, rdr.BaseStream.Position);
            foreach (var folder in ret) folder.FileNameBlock = filenameBlock;
        }

        return ret;
    }

    private Dictionary<string, FolderRecord> GetFolderDictionary()
    {
        if (!HasFolderNames)
            throw new ArgumentException("Cannot get folders by name if the BSA does not have folder names.");
        return _folders.Value.ToDictionary(folder => folder.Name!);
    }
}