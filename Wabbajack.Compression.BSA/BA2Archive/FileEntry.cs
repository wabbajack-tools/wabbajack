using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Wabbajack.Common;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class FileEntry : IBA2FileEntry
{
    internal uint _align;
    internal Reader _bsa;
    internal uint _dirHash;
    internal string _extension;
    internal uint _flags;
    internal int _index;
    internal uint _nameHash;
    internal ulong _offset;
    internal uint _realSize;
    internal uint _size;

    public FileEntry(Reader ba2Reader, int index)
    {
        _index = index;
        _bsa = ba2Reader;
        var _rdr = ba2Reader._rdr;
        _nameHash = _rdr.ReadUInt32();
        FullPath = _nameHash.ToString("X");
        _extension = Encoding.UTF8.GetString(_rdr.ReadBytes(4));
        _dirHash = _rdr.ReadUInt32();
        _flags = _rdr.ReadUInt32();
        _offset = _rdr.ReadUInt64();
        _size = _rdr.ReadUInt32();
        _realSize = _rdr.ReadUInt32();
        _align = _rdr.ReadUInt32();
    }

    public bool Compressed => _size != 0;


    public string FullPath { get; set; }

    public RelativePath Path => FullPath.ToRelativePath();

    public uint Size => _realSize;

    public AFile State => new BA2File
    {
        NameHash = _nameHash,
        DirHash = _dirHash,
        Flags = _flags,
        Align = _align,
        Compressed = Compressed,
        Path = Path,
        Extension = _extension,
        Index = _index
    };

    public async ValueTask CopyDataTo(Stream output, CancellationToken token)
    {
        await using var fs = await _bsa._streamFactory.GetStream();
        fs.Seek((long) _offset, SeekOrigin.Begin);
        var len = Compressed ? _size : _realSize;

        var bytes = new byte[len];
        await fs.ReadAsync(bytes.AsMemory(0, (int) len), token);

        if (!Compressed)
        {
            await output.WriteAsync(bytes, token);
        }
        else
        {
            var uncompressed = new byte[_realSize];
            var inflater = new Inflater();
            inflater.SetInput(bytes);
            inflater.Inflate(uncompressed);
            await output.WriteAsync(uncompressed, token);
        }

        await output.FlushAsync(token);
    }

    public async ValueTask<IStreamFactory> GetStreamFactory(CancellationToken token)
    {
        var ms = new MemoryStream();
        await CopyDataTo(ms, token);
        ms.Position = 0;
        return new MemoryStreamFactory(ms, Path, _bsa._streamFactory.LastModifiedUtc);
    }
}