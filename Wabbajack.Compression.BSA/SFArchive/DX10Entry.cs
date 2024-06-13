using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.FO4Archive;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;

namespace Wabbajack.Compression.BSA.SFArchive;

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
    private uint _unk0;
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
        _unk0 = _rdr.ReadUInt32();
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

    public uint HeaderSize => DDS.HeaderSizeForFormat((DXGI_FORMAT) _format);

    public string FullPath { get; set; }

    public RelativePath Path => FullPath.ToRelativePath();
    public uint Size => (uint) _chunks.Sum(f => f._fullSz) + HeaderSize + sizeof(uint);

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

            br.BaseStream.Seek((long) chunk._offset, SeekOrigin.Begin);

            if (!isCompressed)
            {
                await br.BaseStream.ReadAsync(full, token);
            }
            else
            {
                var compressed = new byte[chunk._packSz];
                await br.BaseStream.ReadAsync(compressed, token);
                var inflater = new Inflater();
                inflater.SetInput(compressed);
                inflater.Inflate(full);
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
        var ddsHeader = new DDS_HEADER();

        ddsHeader.dwSize = ddsHeader.GetSize();
        ddsHeader.dwHeaderFlags = DDS.DDS_HEADER_FLAGS_TEXTURE | DDS.DDS_HEADER_FLAGS_LINEARSIZE |
                                  DDS.DDS_HEADER_FLAGS_MIPMAP;
        ddsHeader.dwHeight = _height;
        ddsHeader.dwWidth = _width;
        ddsHeader.dwMipMapCount = _numMips;
        ddsHeader.PixelFormat.dwSize = ddsHeader.PixelFormat.GetSize();
        ddsHeader.dwDepth = 1;
        ddsHeader.dwSurfaceFlags = DDS.DDS_SURFACE_FLAGS_TEXTURE | DDS.DDS_SURFACE_FLAGS_MIPMAP;
        ddsHeader.dwCubemapFlags = _isCubemap == 1 ? (uint)(DDSCAPS2.CUBEMAP
                   | DDSCAPS2.CUBEMAP_NEGATIVEX | DDSCAPS2.CUBEMAP_POSITIVEX
                   | DDSCAPS2.CUBEMAP_NEGATIVEY | DDSCAPS2.CUBEMAP_POSITIVEY
                   | DDSCAPS2.CUBEMAP_NEGATIVEZ | DDSCAPS2.CUBEMAP_POSITIVEZ
                   | DDSCAPS2.CUBEMAP_ALLFACES) : 0u;
        

        switch ((DXGI_FORMAT) _format)
        {
            case DXGI_FORMAT.BC1_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '1');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height / 2); // 4bpp
                break;
            case DXGI_FORMAT.BC2_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '3');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height); // 8bpp
                break;
            case DXGI_FORMAT.BC3_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '5');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height); // 8bpp
                break;
            case DXGI_FORMAT.BC5_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                if (_bsa.UseATIFourCC)
                    ddsHeader.PixelFormat.dwFourCC =
                        DDS.MAKEFOURCC('A', 'T', 'I',
                            '2'); // this is more correct but the only thing I have found that supports it is the nvidia photoshop plugin
                else
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('B', 'C', '5', 'U');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height); // 8bpp
                break;
            case DXGI_FORMAT.BC1_UNORM_SRGB:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height / 2); // 4bpp
                break;
            case DXGI_FORMAT.BC3_UNORM_SRGB:
            case DXGI_FORMAT.BC6H_UF16:
            case DXGI_FORMAT.BC4_UNORM:
            case DXGI_FORMAT.BC5_SNORM:
            case DXGI_FORMAT.BC7_UNORM:
            case DXGI_FORMAT.BC7_UNORM_SRGB:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height); // 8bpp
                break;
            case DXGI_FORMAT.R8G8B8A8_UNORM:
            case DXGI_FORMAT.R8G8B8A8_UNORM_SRGB:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                ddsHeader.PixelFormat.dwRGBBitCount = 32;
                ddsHeader.PixelFormat.dwRBitMask = 0x000000FF;
                ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                ddsHeader.PixelFormat.dwBBitMask = 0x00FF0000;
                ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height * 4); // 32bpp
                break;
            case DXGI_FORMAT.B8G8R8A8_UNORM:
            case DXGI_FORMAT.B8G8R8X8_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                ddsHeader.PixelFormat.dwRGBBitCount = 32;
                ddsHeader.PixelFormat.dwRBitMask = 0x00FF0000;
                ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                ddsHeader.PixelFormat.dwBBitMask = 0x000000FF;
                ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height * 4); // 32bpp
                break;
            case DXGI_FORMAT.R8_UNORM:
                ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGB;
                ddsHeader.PixelFormat.dwRGBBitCount = 8;
                ddsHeader.PixelFormat.dwRBitMask = 0xFF;
                ddsHeader.dwPitchOrLinearSize = (uint) (_width * _height); // 8bpp
                break;
            default:
                throw new Exception("Unsupported DDS header format. File: " + FullPath);
        }

        bw.Write((uint) DDS.DDS_MAGIC);
        ddsHeader.Write(bw);

        switch ((DXGI_FORMAT) _format)
        {
            case DXGI_FORMAT.BC1_UNORM_SRGB:
            case DXGI_FORMAT.BC3_UNORM_SRGB:
            case DXGI_FORMAT.BC4_UNORM:
            case DXGI_FORMAT.BC5_SNORM:
            case DXGI_FORMAT.BC6H_UF16:
            case DXGI_FORMAT.BC7_UNORM:
            case DXGI_FORMAT.BC7_UNORM_SRGB:
                var dxt10 = new DDS_HEADER_DXT10
                {
                    dxgiFormat = _format,
                    resourceDimension = (uint) DXT10_RESOURCE_DIMENSION.DIMENSION_TEXTURE2D,
                    miscFlag = 0,
                    arraySize = 1,
                    miscFlags2 = DDS.DDS_ALPHA_MODE_UNKNOWN
                };
                dxt10.Write(bw);
                break;
        }
    }
}
