using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;
#nullable disable

namespace Compression.BSA
{
    public enum EntryType
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
        private Stream _stream;
        internal BinaryReader _rdr;
        internal uint _version;
        internal string _headerMagic;
        internal EntryType _type;
        internal uint _numFiles;
        internal ulong _nameTableOffset;
        public IStreamFactory _streamFactory;
        public bool UseATIFourCC { get; set; } = false;

        public bool HasNameTable => _nameTableOffset > 0;

        
        
        public static async Task<BA2Reader> Load(IStreamFactory streamFactory)
        {
            var rdr = new BA2Reader(await streamFactory.GetStream()) {_streamFactory = streamFactory};
            await rdr.LoadHeaders();
            return rdr;
        }

        private BA2Reader(Stream stream)
        {
            _stream = stream;
            _rdr = new BinaryReader(_stream, Encoding.UTF8);
        }

        private async Task LoadHeaders()
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
                        files.Add(new BA2FileEntry(this, idx));
                        break;
                    case EntryType.DX10:
                        files.Add(new BA2DX10Entry(this, idx));
                        break;
                    case EntryType.GNMF:
                        break;

                }
            }

            if (HasNameTable)
            {
                _rdr.BaseStream.Seek((long) _nameTableOffset, SeekOrigin.Begin);
                foreach (var file in files)
                    file.FullPath = Encoding.UTF8.GetString(_rdr.ReadBytes(_rdr.ReadInt16()));
            }
            Files = files;

        }

        public async ValueTask DisposeAsync()
        {
            _stream?.Dispose();
            _rdr?.Dispose();
        }

        public IEnumerable<IFile> Files { get; private set; }
        public ArchiveStateObject State => new BA2StateObject(this);
        public void Dump(Action<string> print)
        {
            print($"HeaderMagic: {_headerMagic}");
            print($"Number of Files: {_numFiles}");
            print($"----------------------------");
            
            foreach (var file in Files)
            {
                print("\n");
                file.Dump(print);
            }
        }
    }

    [JsonName("BA2State")]
    public class BA2StateObject : ArchiveStateObject
    {
        public BA2StateObject()
        {
        }

        public BA2StateObject(BA2Reader ba2Reader)
        {
            Version = ba2Reader._version;
            HeaderMagic = ba2Reader._headerMagic;
            Type = ba2Reader._type;
            HasNameTable = ba2Reader.HasNameTable;
        }

        public bool HasNameTable { get; set; }
        public EntryType Type { get; set; }
        public string HeaderMagic { get; set; }
        public uint Version { get; set; }
        public override async Task<IBSABuilder> MakeBuilder(long size)
        {
            return await BA2Builder.Create(this, size);
        }
    }

    public class BA2DX10Entry : IFileEntry
    {
        internal uint _nameHash;
        internal string _extension;
        internal uint _dirHash;
        internal byte _unk8;
        internal byte _numChunks;
        internal ushort _chunkHdrLen;
        internal ushort _height;
        internal ushort _width;
        internal byte _numMips;
        internal byte _format;
        internal ushort _unk16;
        internal List<BA2TextureChunk> _chunks;
        private BA2Reader _bsa;
        internal int _index;

        public BA2DX10Entry(BA2Reader ba2Reader, int idx)
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
            _unk16 = _rdr.ReadUInt16();
            _index = idx;

            _chunks = Enumerable.Range(0, _numChunks)
                .Select(_ => new BA2TextureChunk(_rdr))
                .ToList();

        }

        public string FullPath { get; set; }

        public RelativePath Path => new RelativePath(FullPath);
        public uint Size => (uint)_chunks.Sum(f => f._fullSz) + HeaderSize + sizeof(uint);
        public FileStateObject State => new BA2DX10EntryState(this);

        public uint HeaderSize => DDS.HeaderSizeForFormat((DXGI_FORMAT)_format);

        public async ValueTask CopyDataTo(Stream output)
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
                    await br.BaseStream.ReadAsync(full, 0, full.Length);
                }
                else
                {
                    byte[] compressed = new byte[chunk._packSz];
                    await br.BaseStream.ReadAsync(compressed, 0, compressed.Length);
                    var inflater = new Inflater();
                    inflater.SetInput(compressed);
                    inflater.Inflate(full);
                }

                await bw.BaseStream.WriteAsync(full, 0, full.Length);
            }
        }

        public void Dump(Action<string> print)
        {
            throw new NotImplementedException();
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
            ddsHeader.dwDepth = 1;
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
                        ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('B', 'C', '5', 'U');
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
                    throw new Exception("Unsupported DDS header format. File: " + FullPath);
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
        
        public async ValueTask<IStreamFactory> GetStreamFactory()
        {
            var ms = new MemoryStream();
            await CopyDataTo(ms);
            ms.Position = 0;
            return new MemoryStreamFactory(ms);
        }
    }

    [JsonName("BA2DX10Entry")]
    public class BA2DX10EntryState : FileStateObject
    {
        public BA2DX10EntryState() { }
        public BA2DX10EntryState(BA2DX10Entry ba2Dx10Entry)
        {
            Path = ba2Dx10Entry.Path;
            NameHash = ba2Dx10Entry._nameHash;
            Extension = ba2Dx10Entry._extension;
            DirHash = ba2Dx10Entry._dirHash;
            Unk8 = ba2Dx10Entry._unk8;
            ChunkHdrLen = ba2Dx10Entry._chunkHdrLen;
            Height = ba2Dx10Entry._height;
            Width = ba2Dx10Entry._width;
            NumMips = ba2Dx10Entry._numMips;
            PixelFormat = ba2Dx10Entry._format;
            Unk16 = ba2Dx10Entry._unk16;
            Index = ba2Dx10Entry._index;
            Chunks = ba2Dx10Entry._chunks.Select(ch => new ChunkState(ch)).ToList();
        }

        public List<ChunkState> Chunks { get; set; }

        public ushort Unk16 { get; set; }

        public byte PixelFormat { get; set; }

        public byte NumMips { get; set; }

        public ushort Width { get; set; }

        public ushort Height { get; set; }

        public ushort ChunkHdrLen { get; set; }

        public byte Unk8 { get; set; }

        public uint DirHash { get; set; }

        public string Extension { get; set; }

        public uint NameHash { get; set; }
    }

    [JsonName("Chunk")]
    public class ChunkState
    {
        public ChunkState() {}
        public ChunkState(BA2TextureChunk ch)
        {
            FullSz = ch._fullSz;
            StartMip = ch._startMip;
            EndMip = ch._endMip;
            Align = ch._align;
            Compressed = ch._packSz != 0;
        }

        public bool Compressed { get; set; }
        public uint Align { get; set; }
        public ushort EndMip { get; set; }
        public ushort StartMip { get; set; }
        public uint FullSz { get; set; }
    }

    [JsonName("BA2TextureChunk")]
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
        internal uint _nameHash;
        internal string _extension;
        internal uint _dirHash;
        internal uint _flags;
        internal ulong _offset;
        internal uint _size;
        internal uint _realSize;
        internal uint _align;
        internal BA2Reader _bsa;
        internal int _index;

        public bool Compressed => _size != 0;
        
        public void Dump(Action<string> print)
        {
            print($"FullPath: {FullPath}");
            print($"Name Hash: {_nameHash}");
            print($"Offset: {_offset}");
            print($"Flags: {_flags:x}");
            print($"Real Size: {_realSize}");
            print($"Index: {_index}");
        }


        public BA2FileEntry(BA2Reader ba2Reader, int index)
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
        
        

        public string FullPath { get; set; }

        public RelativePath Path => new RelativePath(FullPath);
        public uint Size => _realSize;
        public FileStateObject State => new BA2FileEntryState(this);

        public async ValueTask CopyDataTo(Stream output)
        {
            await using var fs = await _bsa._streamFactory.GetStream();
            fs.Seek((long) _offset, SeekOrigin.Begin);
            uint len = Compressed ? _size : _realSize;

            var bytes = new byte[len];
            fs.Read(bytes, 0, (int) len);

            if (!Compressed)
            {
                await output.WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                var uncompressed = new byte[_realSize];
                var inflater = new Inflater();
                inflater.SetInput(bytes);
                inflater.Inflate(uncompressed);
                await output.WriteAsync(uncompressed, 0, uncompressed.Length);
            }
        }
        
        public async ValueTask<IStreamFactory> GetStreamFactory()
        {
            var ms = new MemoryStream();
            await CopyDataTo(ms);
            ms.Position = 0;
            return new MemoryStreamFactory(ms);
        }
    }

    [JsonName("BA2FileEntryState")]
    public class BA2FileEntryState : FileStateObject
    {
        public BA2FileEntryState() { }

        public BA2FileEntryState(BA2FileEntry ba2FileEntry)
        {
            NameHash = ba2FileEntry._nameHash;
            DirHash = ba2FileEntry._dirHash;
            Flags = ba2FileEntry._flags;
            Align = ba2FileEntry._align;
            Compressed = ba2FileEntry.Compressed;
            Path = ba2FileEntry.Path;
            Extension = ba2FileEntry._extension;
            Index = ba2FileEntry._index;
        }

        public string Extension { get; set; }
        public bool Compressed { get; set; }
        public uint Align { get; set; }
        public uint Flags { get; set; }
        public uint DirHash { get; set; }
        public uint NameHash { get; set; }
    }
}
