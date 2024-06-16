using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DirectXTex;
using ICSharpCode.SharpZipLib.Zip.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.BA2Archive;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class DX10Entry : IBA2FileEntry
{
    private readonly Reader _bsa;
    private ushort _chunkHdrLen;
    private List<TextureChunk> _chunks;
    private uint _dirHash;
    private string _extension;
    private byte _format;
    private ushort _height;
    private int _index;
    private uint _nameHash;
    private byte _numChunks;
    private byte _numMips;
    private ushort _unk16;
    private byte _unk8;
    private ushort _width;
    private readonly byte _isCubemap;
    private readonly byte _tileMode;

    public DX10Entry(Reader ba2Reader, int idx)
    {
        _bsa = ba2Reader;
        var _rdr = ba2Reader._rdr;
        _nameHash = _rdr.ReadUInt32();
        FullPath = _nameHash.ToString("X");
        _extension = Encoding.UTF8.GetString(_rdr.ReadBytes(4));
        _dirHash = _rdr.ReadUInt32();
        _unk8 = _rdr.ReadByte();
        _numChunks = _rdr.ReadByte();
        _chunkHdrLen = _rdr.ReadUInt16();
        _height = _rdr.ReadUInt16();
        _width = _rdr.ReadUInt16();
        _numMips = _rdr.ReadByte();
        _format = _rdr.ReadByte();
        _isCubemap = _rdr.ReadByte();
        _tileMode = _rdr.ReadByte();
        _index = idx;

        _chunks = Enumerable.Range(0, _numChunks)
            .Select(_ => new TextureChunk(_rdr))
            .ToList();
    }
    private DirectXTexUtility.TexMetadata? _metadata = null;

    public DirectXTexUtility.TexMetadata Metadata
    {
        get
        {
            if (_metadata == null)
                _metadata = DirectXTexUtility.GenerateMetadata(_width, _height, _numMips, (DirectXTexUtility.DXGIFormat)_format, _isCubemap == 1);
            return (DirectXTexUtility.TexMetadata)_metadata;
        }
    }

    private uint _headerSize = 0;
    public uint HeaderSize
    {
        get
        {
            if (_headerSize > 0)
                return _headerSize;
            uint size = 0;
            size += (uint)Marshal.SizeOf(DirectXTexUtility.DDSHeader.DDSMagic);
            size += (uint)Marshal.SizeOf<DirectXTexUtility.DDSHeader>();
            var pixelFormat = DirectXTexUtility.GetPixelFormat(Metadata);
            var hasDx10Header = DirectXTexUtility.HasDx10Header(pixelFormat);
            if (hasDx10Header)
                size += (uint)Marshal.SizeOf<DirectXTexUtility.DX10Header>();

            return _headerSize = size;
        }
    }

    public string FullPath { get; set; }

    public RelativePath Path => FullPath.ToRelativePath();
    public uint Size => (uint)_chunks.Sum(f => f._fullSz) + HeaderSize;

    public AFile State => new BA2DX10File
    {
        Path = Path,
        NameHash = _nameHash,
        Extension = _extension,
        DirHash = _dirHash,
        Unk8 = _unk8,
        ChunkHdrLen = _chunkHdrLen,
        Height = _height,
        Width = _width,
        NumMips = _numMips,
        PixelFormat = _format,
        IsCubeMap = _isCubemap,
        TileMode = _tileMode,
        Index = _index,
        Chunks = _chunks.Select(ch => new BA2Chunk
        {
            FullSz = ch._fullSz,
            StartMip = ch._startMip,
            EndMip = ch._endMip,
            Align = ch._align,
            Compressed = ch._packSz != 0
        }).ToArray()
    };

    public async ValueTask CopyDataTo(Stream output, CancellationToken token)
    {
        var bw = new BinaryWriter(output);

        WriteHeader(bw);

        await using var fs = await _bsa._streamFactory.GetStream();
        using var br = new BinaryReader(fs);
        foreach (var chunk in _chunks)
        {
            var full = new byte[chunk._fullSz];
            var isCompressed = chunk._packSz != 0;

            br.BaseStream.Seek((long)chunk._offset, SeekOrigin.Begin);

            if (!isCompressed)
            {
                await br.BaseStream.ReadAsync(full, token);
            }
            else
            {
                var compressed = new byte[chunk._packSz];
                await br.BaseStream.ReadAsync(compressed, token);
                if (_bsa._compression == 3)
                {
                    LZ4Codec.PartialDecode(compressed, full);
                }
                else
                {
                    var inflater = new Inflater();
                    inflater.SetInput(compressed);
                    inflater.Inflate(full);
                }
            }

            await bw.BaseStream.WriteAsync(full, token);
        }
    }


    public async ValueTask<IStreamFactory> GetStreamFactory(CancellationToken token)
    {
        var ms = new MemoryStream();
        await CopyDataTo(ms, token);
        ms.Position = 0;
        return new MemoryStreamFactory(ms, Path, _bsa._streamFactory.LastModifiedUtc);
    }

    private void WriteHeader(BinaryWriter bw)
    {
        DirectXTexUtility.GenerateDDSHeader(Metadata, DirectXTexUtility.DDSFlags.FORCEDX10EXTMISC2, out var header, out var header10);
        var headerBytes = DirectXTexUtility.EncodeDDSHeader(header, header10);
        bw.Write(headerBytes);
    }
}

