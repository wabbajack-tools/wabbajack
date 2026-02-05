using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.Compression.BSA.TES3Archive;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compression.BSA.TES5Archive;

public class Builder : IBuilder
{
    internal byte[] _fileId;

    private List<FileEntry> _files = new();
    internal List<FolderRecordBuilder> _folders = new();
    internal uint _offset;
    internal DiskSlabAllocator _slab;
    internal uint _totalFileNameLength;

    public IEnumerable<FileEntry> Files => _files;

    public ArchiveFlags ArchiveFlags { get; set; }

    public FileFlags FileFlags { get; set; }

    public VersionType HeaderType { get; set; }

    public IEnumerable<RelativePath> FolderNames
    {
        get { return _files.Select(f => f.Path.Level == 1 ? default : f.Path.Parent).Distinct(); }
    }

    public bool HasFolderNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNames);

    public bool HasFileNames => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNames);

    public bool CompressedByDefault => ArchiveFlags.HasFlag(ArchiveFlags.Compressed);

    public bool HasNameBlobs => ArchiveFlags.HasFlag(ArchiveFlags.HasFileNameBlobs);

    public async ValueTask AddFile(AFile state, Stream src, CancellationToken token)
    {
        var bsaState = (BSAFile) state;

        var r = await FileEntry.Create(this, bsaState.Path, src, bsaState.FlipCompression, token);

        lock (this)
        {
            _files.Add(r);
        }
    }

    public async ValueTask Build(Stream fs, CancellationToken token)
    {
        RegenFolderRecords();
        await using var wtr = new BinaryWriter(fs, Encoding.Default, true);

        wtr.Write(_fileId);
        wtr.Write((uint) HeaderType);
        wtr.Write(_offset);
        wtr.Write((uint) ArchiveFlags);
        var folders = FolderNames.ToList();
        wtr.Write((uint) folders.Count);
        wtr.Write((uint) _files.Count);
        wtr.Write((uint) _folders.Select(f => f._nameBytes.Length - 1).Sum()); // totalFolderNameLength
        var s = _files.Select(f => f._pathBytes.Length).Sum();
        _totalFileNameLength = (uint) _files.Select(f => f._nameBytes.Length).Sum();
        wtr.Write(_totalFileNameLength); // totalFileNameLength
        wtr.Write((uint) FileFlags);

        foreach (var folder in _folders) folder.WriteFolderRecord(wtr);

        foreach (var folder in _folders)
        {
            if (HasFolderNames)
                wtr.Write(folder._nameBytes);
            foreach (var file in folder._files) file.WriteFileRecord(wtr);
        }

        foreach (var file in _files)
            await wtr.BaseStream.WriteAsync(file._nameBytes, token);

        foreach (var file in _files)
            await file.WriteData(wtr, token);
    }

    public static Builder Create(TemporaryFileManager tempGenerator)
    {
        var self = new Builder
        {
            _fileId = Encoding.ASCII.GetBytes("BSA\0"),
            _offset = 0x24,
            _slab = new DiskSlabAllocator(tempGenerator)
        };
        return self;
    }

    public static Builder Create(BSAState bsaStateObject, TemporaryFileManager tempGenerator)
    {
        var self = Create(tempGenerator);
        self.HeaderType = (VersionType) bsaStateObject.Version;
        self.FileFlags = (FileFlags) bsaStateObject.FileFlags;
        self.ArchiveFlags = (ArchiveFlags) bsaStateObject.ArchiveFlags;
        return self;
    }

    public async ValueTask DisposeAsync()
    {
        await _slab.DisposeAsync();
    }

    public void RegenFolderRecords()
    {
        _folders = _files.GroupBy(f => f.Path.Level == 1 ? default : f.Path.Parent)
            .Select(f => new FolderRecordBuilder(this, f.Key, f.ToList()))
            .OrderBy(f => f._hash)
            .ToList();

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
    internal Builder _bsa;
    internal uint _fileCount;
    internal IEnumerable<FileEntry> _files;
    internal ulong _hash;
    internal byte[] _nameBytes;
    internal ulong _offset;
    internal uint _recordSize;

    public FolderRecordBuilder(Builder bsa, RelativePath folderName, IEnumerable<FileEntry> files)
    {
        _files = files.OrderBy(f => f._hash);
        Name = folderName;
        _bsa = bsa;
        // Folders don't have extensions, so let's make sure we cut it out
        _hash = Name.GetFolderBSAHash();
        _fileCount = (uint) files.Count();
        _nameBytes = folderName.ToBZString(_bsa.HeaderType);
        _recordSize = sizeof(ulong) + sizeof(uint) + sizeof(uint);
    }

    public ulong Hash => _hash;

    public RelativePath Name { get; }

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
        else if (_bsa.HeaderType is VersionType.FO3 or VersionType.TES4)
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
    internal Builder _bsa;
    internal bool _flipCompression;
    internal FolderRecordBuilder _folder;

    internal ulong _hash;
    internal string _name;
    internal byte[] _nameBytes;
    private long _offsetOffset;
    internal int _originalSize;
    internal RelativePath _path;
    private byte[] _pathBSBytes;
    internal byte[] _pathBytes;
    private Stream _srcData;

    public bool Compressed
    {
        get
        {
            if (_flipCompression)
                return !_bsa.CompressedByDefault;
            return _bsa.CompressedByDefault;
        }
    }

    public RelativePath Path => _path;

    public bool FlipCompression => _flipCompression;

    public ulong Hash => _hash;

    public FolderRecordBuilder Folder => _folder;

    public static async Task<FileEntry> Create(Builder bsa, RelativePath path, Stream src, bool flipCompression,
        CancellationToken token)
    {
        var entry = new FileEntry();
        entry._bsa = bsa;
        entry._path = path;
        entry._name = (string) entry._path.FileName;
        entry._hash = entry._name.GetBSAHash();
        entry._nameBytes = entry._name.ToTermString(bsa.HeaderType);
        entry._pathBytes = entry._path.ToTermString(bsa.HeaderType);
        entry._pathBSBytes = entry._path.ToBSString();
        entry._flipCompression = flipCompression;
        entry._srcData = src;

        entry._originalSize = (int) entry._srcData.Length;

        if (entry.Compressed)
            await entry.CompressData(token);
        return entry;
    }

    private async Task CompressData(CancellationToken token)
    {
        switch (_bsa.HeaderType)
        {
            case VersionType.SSE:
            {
                var r = new MemoryStream();
                await using (var w = LZ4Stream.Encode(r,
                    new LZ4EncoderSettings {CompressionLevel = LZ4Level.L12_MAX}, true))
                {
                    await _srcData.CopyToWithStatusAsync(_srcData.Length, w, token);
                }

                await _srcData.DisposeAsync();
                _srcData = _bsa._slab.Allocate(r.Length);
                r.Position = 0;
                await r.CopyToWithStatusAsync(r.Length, _srcData, token);
                _srcData.Position = 0;
                break;
            }
            case VersionType.FO3:
            case VersionType.TES4:
            {
                var r = new MemoryStream();
                using (var w = new DeflaterOutputStream(r))
                {
                    w.IsStreamOwner = false;
                    await _srcData.CopyToWithStatusAsync(_srcData.Length, w, token);
                }

                await _srcData.DisposeAsync();
                _srcData = _bsa._slab.Allocate(r.Length);
                r.Position = 0;
                await r.CopyToWithStatusAsync(r.Length, _srcData, token);
                _srcData.Position = 0;
                break;
            }
            default:
                throw new NotImplementedException($"Can't compress data for {_bsa.HeaderType} BSAs.");
        }
    }

    internal void WriteFileRecord(BinaryWriter wtr)

    {
        wtr.Write(_hash);
        var size = _srcData.Length;
        if (_bsa.HasNameBlobs) size += _pathBSBytes.Length;
        if (Compressed) size += 4;
        if (_flipCompression)
            wtr.Write((uint) size | (0x1 << 30));
        else
            wtr.Write((uint) size);

        _offsetOffset = wtr.BaseStream.Position;
        wtr.Write(0xDEADBEEF);
    }

    internal async Task WriteData(BinaryWriter wtr, CancellationToken token)
    {
        var offset = (uint) wtr.BaseStream.Position;
        wtr.BaseStream.Position = _offsetOffset;
        wtr.Write(offset);
        wtr.BaseStream.Position = offset;

        if (_bsa.HasNameBlobs) wtr.Write(_pathBSBytes);

        if (Compressed)
        {
            wtr.Write((uint) _originalSize);
            _srcData.Position = 0;
            await _srcData.CopyToLimitAsync(wtr.BaseStream, (int) _srcData.Length, token);
            await _srcData.DisposeAsync();
        }
        else
        {
            _srcData.Position = 0;
            await _srcData.CopyToLimitAsync(wtr.BaseStream, (int) _srcData.Length, token);
            await _srcData.DisposeAsync();
        }
    }
}