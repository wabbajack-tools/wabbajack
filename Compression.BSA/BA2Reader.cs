using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using ICSharpCode.SharpZipLib.Zip.Compression;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    enum EntryType
    {
        GNRL,
        DX10,
        GNMF
    }

    interface IFileEntry : IFile
    {
        string FullPath { get; set; }

    }

    public class BA2Reader : IBSAReader
    {
        internal string _filename;
        private Stream _stream;
        internal BinaryReader _rdr;
        private uint _version;
        private string _headerMagic;
        private EntryType _type;
        private uint _numFiles;
        private ulong _nameTableOffset;
        public bool UseATIFourCC { get; set; } = false;

        public bool HasNameTable => _nameTableOffset > 0;

        public BA2Reader(string filename) : this(File.OpenRead(filename))
        {
            _filename = filename;
        }

        public BA2Reader(Stream stream)
        {
            _stream = stream;
            _rdr = new BinaryReader(_stream, Encoding.UTF7);
            LoadHeaders();
        }

        public void LoadHeaders()
        {
            _headerMagic = Encoding.ASCII.GetString(_rdr.ReadBytes(4));

            if (_headerMagic != "BTDX")
                throw new InvalidDataException("Unknown header type: " + _headerMagic);

            _version = _rdr.ReadUInt32();
            
            string fourcc = Encoding.ASCII.GetString(_rdr.ReadBytes(4));

            if (Enum.TryParse(fourcc, out EntryType entryType))
            {
                _type = entryType;
            }
            else
            {
                throw new InvalidDataException($"Can't parse entry types of {fourcc}");
            }

            _numFiles = _rdr.ReadUInt32();
            _nameTableOffset = _rdr.ReadUInt64();

            var files = new List<IFileEntry>();
            for (var idx = 0; idx < _numFiles; idx += 1)
            {
                switch (_type)
                {
                    case EntryType.GNRL:
                        files.Add(new BA2FileEntry(this));
                        break;
                    case EntryType.DX10:
                        files.Add(new BA2DX10Entry(this));
                        break;
                    case EntryType.GNMF:
                        break;

                }
            }

            if (HasNameTable)
            {
                _rdr.BaseStream.Seek((long) _nameTableOffset, SeekOrigin.Begin);
                foreach (var file in files)
                    file.FullPath = Encoding.UTF7.GetString(_rdr.ReadBytes(_rdr.ReadInt16()));
            }
            Files = files;

        }

        public void Dispose()
        {
            _stream?.Dispose();
            _rdr?.Dispose();
        }

        public IEnumerable<IFile> Files { get; private set; }
    }

    public class BA2DX10Entry : IFileEntry
    {
        private uint _nameHash;
        private string _extension;
        private uint _dirHash;
        private byte _unk8;
        private byte _numChunks;
        private ushort _chunkHdrLen;
        private ushort _height;
        private ushort _width;
        private byte _numMips;
        private byte _format;
        private ushort _unk16;
        private List<BA2TextureChunk> _chunks;
        private BA2Reader _bsa;

        public BA2DX10Entry(BA2Reader ba2Reader)
        {
            _bsa = ba2Reader;
            var _rdr = ba2Reader._rdr;
            _nameHash = _rdr.ReadUInt32();
            FullPath = _nameHash.ToString("X");
            _extension = Encoding.UTF7.GetString(_rdr.ReadBytes(4));
            _dirHash = _rdr.ReadUInt32();
            _unk8 = _rdr.ReadByte();
            _numChunks = _rdr.ReadByte();
            _chunkHdrLen = _rdr.ReadUInt16();
            _height = _rdr.ReadUInt16();
            _width = _rdr.ReadUInt16();
            _numMips = _rdr.ReadByte();
            _format = _rdr.ReadByte();
            _unk16 = _rdr.ReadUInt16();

            _chunks = Enumerable.Range(0, _numChunks)
                .Select(idx => new BA2TextureChunk(_rdr))
                .ToList();

        }

        public string FullPath { get; set; }

        public string Path => FullPath;
        public uint Size => (uint)_chunks.Sum(f => f._fullSz) + HeaderSize + sizeof(uint);

        public uint HeaderSize 
        {
            get
            {
                unsafe
                {
                    switch ((DXGI_FORMAT) _format)
                    {
                        case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB:
                        case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB:
                        case DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM:
                        case DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM:
                        case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16:
                        case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
                        case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB:
                            return DDS_HEADER_DXT10.Size + DDS_HEADER.Size;
                        default:
                            return DDS_HEADER.Size;
                    }
                }
            }
        }

        public void CopyDataTo(Stream output)
        {
            var bw = new BinaryWriter(output);

            WriteHeader(bw);

            using (var fs = File.OpenRead(_bsa._filename))
            using (var br = new BinaryReader(fs))
            {
                foreach (var chunk in _chunks)
                {
                    byte[] full = new byte[chunk._fullSz];
                    var isCompressed = chunk._packSz != 0;

                    br.BaseStream.Seek((long)chunk._offset, SeekOrigin.Begin);

                    if (!isCompressed)
                    {
                        br.Read(full, 0, full.Length);
                    }
                    else
                    {
                        byte[] compressed = new byte[chunk._packSz];
                        br.Read(compressed, 0, compressed.Length);
                        var inflater = new Inflater();
                        inflater.SetInput(compressed);
                        inflater.Inflate(full);
                    }

                    bw.Write(full);
                }
            }

        }
        private void WriteHeader(BinaryWriter bw)
        {
            var ddsHeader = new DDS_HEADER();

            ddsHeader.dwSize = ddsHeader.GetSize();
            ddsHeader.dwHeaderFlags = DDS.DDS_HEADER_FLAGS_TEXTURE | DDS.DDS_HEADER_FLAGS_LINEARSIZE | DDS.DDS_HEADER_FLAGS_MIPMAP;
            ddsHeader.dwHeight = _height;
            ddsHeader.dwWidth = _width;
            ddsHeader.dwMipMapCount = _numMips;
            ddsHeader.PixelFormat.dwSize = ddsHeader.PixelFormat.GetSize();
            ddsHeader.dwSurfaceFlags = DDS.DDS_SURFACE_FLAGS_TEXTURE | DDS.DDS_SURFACE_FLAGS_MIPMAP;

            switch ((DXGI_FORMAT)_format)
            {
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '1');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height / 2); // 4bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '3');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height); // 8bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '5');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height); // 8bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    if (_bsa.UseATIFourCC)
                        ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('A', 'T', 'I', '2'); // this is more correct but the only thing I have found that supports it is the nvidia photoshop plugin
                    else
                        ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '5');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height); // 8bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height / 2); // 4bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16:
                case DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height); // 8bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height * 4); // 32bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height * 4); // 32bpp
                    break;
                case DXGI_FORMAT.DXGI_FORMAT_R8_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGB;
                    ddsHeader.PixelFormat.dwRGBBitCount = 8;
                    ddsHeader.PixelFormat.dwRBitMask = 0xFF;
                    ddsHeader.dwPitchOrLinearSize = (uint)(_width * _height); // 8bpp
                    break;
                default:
                    throw new Exception("Unsupported DDS header format. File: " + this.FullPath);
            }

            bw.Write((uint)DDS.DDS_MAGIC);
            ddsHeader.Write(bw);

            switch ((DXGI_FORMAT)_format)
            {
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB:
                    var dxt10 = new DDS_HEADER_DXT10()
                    {
                        dxgiFormat = _format,
                        resourceDimension = (uint)DXT10_RESOURCE_DIMENSION.DIMENSION_TEXTURE2D,
                        miscFlag = 0,
                        arraySize = 1,
                        miscFlags2 = DDS.DDS_ALPHA_MODE_UNKNOWN
                    };
                    dxt10.Write(bw);
                    break;
            }
        }
    }

    public class BA2TextureChunk
    {
        internal ulong _offset;
        internal uint _packSz;
        internal uint _fullSz;
        internal ushort _startMip;
        internal ushort _endMip;
        internal uint _align;

        public BA2TextureChunk(BinaryReader rdr)
        {
            _offset = rdr.ReadUInt64();
            _packSz = rdr.ReadUInt32();
            _fullSz = rdr.ReadUInt32();
            _startMip = rdr.ReadUInt16();
            _endMip = rdr.ReadUInt16();
            _align = rdr.ReadUInt32();
        }
    }

    public class BA2FileEntry : IFileEntry
    {
        private uint _nameHash;
        private string _extension;
        private uint _dirHash;
        private uint _flags;
        private ulong _offset;
        private uint _size;
        private uint _realSize;
        private uint _align;
        private BA2Reader _bsa;

        private bool Compressed => _size != 0;

        public BA2FileEntry(BA2Reader ba2Reader)
        {
            _bsa = ba2Reader;
            var _rdr = ba2Reader._rdr;
            _nameHash = _rdr.ReadUInt32();
            FullPath = _nameHash.ToString("X");
            _extension = Encoding.UTF7.GetString(_rdr.ReadBytes(4));
            _dirHash = _rdr.ReadUInt32();
            _flags = _rdr.ReadUInt32();
            _offset = _rdr.ReadUInt64();
            _size = _rdr.ReadUInt32();
            _realSize = _rdr.ReadUInt32();
            _align = _rdr.ReadUInt32();
        }

        public string FullPath { get; set; }

        public string Path => FullPath;
        public uint Size => _realSize;

        public void CopyDataTo(Stream output)
        {
            using (var bw = new BinaryWriter(output))
            using (var fs = File.OpenRead(_bsa._filename))
            using (var br = new BinaryReader(fs))
            {
                br.BaseStream.Seek((long) _offset, SeekOrigin.Begin);
                uint len = Compressed ? _size : _realSize;

                var bytes = new byte[len];
                br.Read(bytes, 0, (int) len);

                if (!Compressed)
                {
                    bw.Write(bytes);
                }
                else
                {
                    var uncompressed = new byte[_realSize];
                    var inflater = new Inflater();
                    inflater.SetInput(bytes);
                    inflater.Inflate(uncompressed);
                    bw.Write(uncompressed);
                }
            }
        }
    }
}
