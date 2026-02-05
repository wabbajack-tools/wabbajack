// ------------------------------------------------------------------------
// DirectXTex Utility - A simple class for generating DDS Headers
// Copyright(c) 2018 Philip/Scobalula
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// ------------------------------------------------------------------------
// Author: Philip/Scobalula
// Description: DirectXTex DDS Header Utilities
// Source: https://gist.github.com/Scobalula/d9474f3fcf3d5a2ca596fceb64e16c98

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DirectXTex
{
    public class DirectXTexUtility
    {
        #region Enumerators
        /// <summary>
        /// DDS Formats
        /// </summary>
        public enum DXGIFormat : uint
        {
            UNKNOWN = 0,
            R32G32B32A32TYPELESS = 1,
            R32G32B32A32FLOAT = 2,
            R32G32B32A32UINT = 3,
            R32G32B32A32SINT = 4,
            R32G32B32TYPELESS = 5,
            R32G32B32FLOAT = 6,
            R32G32B32UINT = 7,
            R32G32B32SINT = 8,
            R16G16B16A16TYPELESS = 9,
            R16G16B16A16FLOAT = 10,
            R16G16B16A16UNORM = 11,
            R16G16B16A16UINT = 12,
            R16G16B16A16SNORM = 13,
            R16G16B16A16SINT = 14,
            R32G32TYPELESS = 15,
            R32G32FLOAT = 16,
            R32G32UINT = 17,
            R32G32SINT = 18,
            R32G8X24TYPELESS = 19,
            D32FLOATS8X24UINT = 20,
            R32FLOATX8X24TYPELESS = 21,
            X32TYPELESSG8X24UINT = 22,
            R10G10B10A2TYPELESS = 23,
            R10G10B10A2UNORM = 24,
            R10G10B10A2UINT = 25,
            R11G11B10FLOAT = 26,
            R8G8B8A8TYPELESS = 27,
            R8G8B8A8UNORM = 28,
            R8G8B8A8UNORMSRGB = 29,
            R8G8B8A8UINT = 30,
            R8G8B8A8SNORM = 31,
            R8G8B8A8SINT = 32,
            R16G16TYPELESS = 33,
            R16G16FLOAT = 34,
            R16G16UNORM = 35,
            R16G16UINT = 36,
            R16G16SNORM = 37,
            R16G16SINT = 38,
            R32TYPELESS = 39,
            D32FLOAT = 40,
            R32FLOAT = 41,
            R32UINT = 42,
            R32SINT = 43,
            R24G8TYPELESS = 44,
            D24UNORMS8UINT = 45,
            R24UNORMX8TYPELESS = 46,
            X24TYPELESSG8UINT = 47,
            R8G8TYPELESS = 48,
            R8G8UNORM = 49,
            R8G8UINT = 50,
            R8G8SNORM = 51,
            R8G8SINT = 52,
            R16TYPELESS = 53,
            R16FLOAT = 54,
            D16UNORM = 55,
            R16UNORM = 56,
            R16UINT = 57,
            R16SNORM = 58,
            R16SINT = 59,
            R8TYPELESS = 60,
            R8UNORM = 61,
            R8UINT = 62,
            R8SNORM = 63,
            R8SINT = 64,
            A8UNORM = 65,
            R1UNORM = 66,
            R9G9B9E5SHAREDEXP = 67,
            R8G8B8G8UNORM = 68,
            G8R8G8B8UNORM = 69,
            BC1TYPELESS = 70,
            BC1UNORM = 71,
            BC1UNORMSRGB = 72,
            BC2TYPELESS = 73,
            BC2UNORM = 74,
            BC2UNORMSRGB = 75,
            BC3TYPELESS = 76,
            BC3UNORM = 77,
            BC3UNORMSRGB = 78,
            BC4TYPELESS = 79,
            BC4UNORM = 80,
            BC4SNORM = 81,
            BC5TYPELESS = 82,
            BC5UNORM = 83,
            BC5SNORM = 84,
            B5G6R5UNORM = 85,
            B5G5R5A1UNORM = 86,
            B8G8R8A8UNORM = 87,
            B8G8R8X8UNORM = 88,
            R10G10B10XRBIASA2UNORM = 89,
            B8G8R8A8TYPELESS = 90,
            B8G8R8A8UNORMSRGB = 91,
            B8G8R8X8TYPELESS = 92,
            B8G8R8X8UNORMSRGB = 93,
            BC6HTYPELESS = 94,
            BC6HUF16 = 95,
            BC6HSF16 = 96,
            BC7TYPELESS = 97,
            BC7UNORM = 98,
            BC7UNORMSRGB = 99,
            AYUV = 100,
            Y410 = 101,
            Y416 = 102,
            NV12 = 103,
            P010 = 104,
            P016 = 105,
            OPAQUE420 = 106,
            YUY2 = 107,
            Y210 = 108,
            Y216 = 109,
            NV11 = 110,
            AI44 = 111,
            IA44 = 112,
            P8 = 113,
            A8P8 = 114,
            B4G4R4A4UNORM = 115,
            FORCEUINT = 0xffffffff
        }

        /// <summary>
        /// DDS Flags
        /// </summary>
        [Flags]
        public enum DDSFlags
        {
            NONE = 0x0,
            LEGACYDWORD = 0x1,
            NOLEGACYEXPANSION = 0x2,
            NOR10B10G10A2FIXUP = 0x4,
            FORCERGB = 0x8,
            NO16BPP = 0x10,
            EXPANDLUMINANCE = 0x20,
            BADDXTNTAILS = 0x40,
            FORCEDX10EXT = 0x10000,
            FORCEDX10EXTMISC2 = 0x20000,
        }

        /// <summary>
        /// Texture Dimension
        /// </summary>
        public enum TexDimension
        {
            TEXTURE1D = 2,
            TEXTURE2D = 3,
            TEXTURE3D = 4,
        }

        /// <summary>
        /// Misc. Texture Flags
        /// </summary>
        public enum TexMiscFlags : uint
        {
            TEXTURECUBE = 0x4,
        };

        /// <summary>
        /// Misc. Texture Flags
        /// </summary>
        public enum TexMiscFlags2 : uint
        {
            TEXMISC2ALPHAMODEMASK = 0x7,
        };

        /// <summary>
        /// Texture Alpha Modes
        /// </summary>
        public enum TexAlphaMode
        {
            UNKNOWN = 0,
            STRAIGHT = 1,
            PREMULTIPLIED = 2,
            OPAQUE = 3,
            CUSTOM = 4,
        };

        /// <summary>
        /// CP Flags
        /// </summary>
        [Flags]
        public enum CPFLAGS
        {
            NONE = 0x0,      // Normal operation
            LEGACYDWORD = 0x1,      // Assume pitch is DWORD aligned instead of BYTE aligned
            PARAGRAPH = 0x2,      // Assume pitch is 16-byte aligned instead of BYTE aligned
            YMM = 0x4,      // Assume pitch is 32-byte aligned instead of BYTE aligned
            ZMM = 0x8,      // Assume pitch is 64-byte aligned instead of BYTE aligned
            PAGE4K = 0x200,    // Assume pitch is 4096-byte aligned instead of BYTE aligned
            BADDXTNTAILS = 0x1000,   // BC formats with malformed mipchain blocks smaller than 4x4
            BPP24 = 0x10000,  // Override with a legacy 24 bits-per-pixel format size
            BPP16 = 0x20000,  // Override with a legacy 16 bits-per-pixel format size
            BPP8 = 0x40000,  // Override with a legacy 8 bits-per-pixel format size
        };
        #endregion

        #region Structs/Classes
        /// <summary>
        /// Common Pixel Formats
        /// </summary>
        public class PixelFormats
        {
            /// <summary>
            /// DDS Pixel Format Size
            /// </summary>
            public static readonly uint Size = (uint)Marshal.SizeOf<DDSHeader.DDSPixelFormat>();

            #region PixelFormatsConstants
            public const uint DDSFOURCC = 0x00000004;  // DDPFFOURCC
            public const uint DDSRGB = 0x00000040;  // DDPFRGB
            public const uint DDSRGBA = 0x00000041;  // DDPFRGB | DDPFALPHAPIXELS
            public const uint DDSLUMINANCE = 0x00020000;  // DDPFLUMINANCE
            public const uint DDSLUMINANCEA = 0x00020001;  // DDPFLUMINANCE | DDPFALPHAPIXELS
            public const uint DDSALPHAPIXELS = 0x00000001;  // DDPFALPHAPIXELS
            public const uint DDSALPHA = 0x00000002;  // DDPFALPHA
            public const uint DDSPAL8 = 0x00000020;  // DDPFPALETTEINDEXED8
            public const uint DDSPAL8A = 0x00000021;  // DDPFPALETTEINDEXED8 | DDPFALPHAPIXELS
            public const uint DDSBUMPDUDV = 0x00080000;  // DDPFBUMPDUDV
            #endregion

            #region DDSPixelFormats
            public static DDSHeader.DDSPixelFormat DXT1 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', 'T', '1'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat DXT2 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', 'T', '2'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat DXT3 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', 'T', '3'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat DXT4 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', 'T', '4'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat DXT5 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', 'T', '5'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat BC4UNORM =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('B', 'C', '4', 'U'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat BC4SNORM =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('B', 'C', '4', 'S'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat BC5UNORM =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('B', 'C', '5', 'U'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat BC5SNORM =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('B', 'C', '5', 'S'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat R8G8B8G8 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('R', 'G', 'B', 'G'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat G8R8G8B8 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('G', 'R', 'G', 'B'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat YUY2 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('Y', 'U', 'Y', '2'), 0, 0, 0, 0, 0);

            public static DDSHeader.DDSPixelFormat A8R8G8B8 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGBA, 0, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);

            public static DDSHeader.DDSPixelFormat X8R8G8B8 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGB, 0, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000);

            public static DDSHeader.DDSPixelFormat A8B8G8R8 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGBA, 0, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);

            public static DDSHeader.DDSPixelFormat X8B8G8R8 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGB, 0, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0x00000000);

            public static DDSHeader.DDSPixelFormat G16R16 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGB, 0, 32, 0x0000ffff, 0xffff0000, 0x00000000, 0x00000000);

            public static DDSHeader.DDSPixelFormat R5G6B5 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGB, 0, 16, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000);

            public static DDSHeader.DDSPixelFormat A1R5G5B5 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGBA, 0, 16, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000);

            public static DDSHeader.DDSPixelFormat A4R4G4B4 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGBA, 0, 16, 0x00000f00, 0x000000f0, 0x0000000f, 0x0000f000);

            public static DDSHeader.DDSPixelFormat R8G8B8 =
                new DDSHeader.DDSPixelFormat(Size, DDSRGB, 0, 24, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000);

            public static DDSHeader.DDSPixelFormat L8 =
                new DDSHeader.DDSPixelFormat(Size, DDSLUMINANCE, 0, 8, 0xff, 0x00, 0x00, 0x00);

            public static DDSHeader.DDSPixelFormat L16 =
                new DDSHeader.DDSPixelFormat(Size, DDSLUMINANCE, 0, 16, 0xffff, 0x0000, 0x0000, 0x0000);

            public static DDSHeader.DDSPixelFormat A8L8 =
                new DDSHeader.DDSPixelFormat(Size, DDSLUMINANCEA, 0, 16, 0x00ff, 0x0000, 0x0000, 0xff00);

            public static DDSHeader.DDSPixelFormat A8L8ALT =
                new DDSHeader.DDSPixelFormat(Size, DDSLUMINANCEA, 0, 8, 0x00ff, 0x0000, 0x0000, 0xff00);

            public static DDSHeader.DDSPixelFormat A8 =
                new DDSHeader.DDSPixelFormat(Size, DDSALPHA, 0, 8, 0x00, 0x00, 0x00, 0xff);

            public static DDSHeader.DDSPixelFormat V8U8 =
                new DDSHeader.DDSPixelFormat(Size, DDSBUMPDUDV, 0, 16, 0x00ff, 0xff00, 0x0000, 0x0000);

            public static DDSHeader.DDSPixelFormat Q8W8V8U8 =
                new DDSHeader.DDSPixelFormat(Size, DDSBUMPDUDV, 0, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);

            public static DDSHeader.DDSPixelFormat V16U16 =
                new DDSHeader.DDSPixelFormat(Size, DDSBUMPDUDV, 0, 32, 0x0000ffff, 0xffff0000, 0x00000000, 0x00000000);

            public static DDSHeader.DDSPixelFormat DX10 =
                new DDSHeader.DDSPixelFormat(Size, DDSFOURCC, MakePixelFormatFourCC('D', 'X', '1', '0'), 0, 0, 0, 0, 0);
            #endregion
        }

        /// <summary>
        /// DDS Header
        /// </summary>
        public struct DDSHeader
        {
            /// <summary>
            /// DDS Header Flags
            /// </summary>
            [Flags]
            public enum HeaderFlags : uint
            {
                TEXTURE = 0x00001007,  // DDSDCAPS | DDSDHEIGHT | DDSDWIDTH | DDSDPIXELFORMAT 
                MIPMAP = 0x00020000,  // DDSDMIPMAPCOUNT
                VOLUME = 0x00800000,  // DDSDDEPTH
                PITCH = 0x00000008,  // DDSDPITCH
                LINEARSIZE = 0x00080000,  // DDSDLINEARSIZE
            }

            /// <summary>
            /// DDS Surface Flags
            /// </summary>
            public enum SurfaceFlags : uint
            {
                TEXTURE = 0x00001000, // DDSCAPSTEXTURE
                MIPMAP = 0x00400008, // DDSCAPSCOMPLEX | DDSCAPSMIPMAP
                CUBEMAP = 0x00000008, // DDSCAPSCOMPLEX
            }

            /// <summary>
            /// DDS Magic/Four CC
            /// </summary>
            public const uint DDSMagic = 0x20534444;

            /// <summary>
            /// DDS Pixel Format
            /// </summary>
            public struct DDSPixelFormat
            {
                public uint Size;
                public uint Flags;
                public uint FourCC;
                public uint RGBBitCount;
                public uint RBitMask;
                public uint GBitMask;
                public uint BBitMask;
                public uint ABitMask;

                /// <summary>
                /// Creates a new DDS Pixel Format
                /// </summary>
                public DDSPixelFormat(uint size, uint flags, uint fourCC, uint rgbBitCount, uint rBitMask, uint gBitMask, uint bBitMask, uint aBitMask)
                {
                    Size = size;
                    Flags = flags;
                    FourCC = fourCC;
                    RGBBitCount = rgbBitCount;
                    RBitMask = rBitMask;
                    GBitMask = gBitMask;
                    BBitMask = bBitMask;
                    ABitMask = aBitMask;
                }

            }

            public uint Size;
            public HeaderFlags Flags;
            public uint Height;
            public uint Width;
            public uint PitchOrLinearSize;
            public uint Depth; // only if DDSHEADERFLAGSVOLUME is set in flags
            public uint MipMapCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public uint[] Reserved1;
            public DDSPixelFormat PixelFormat;
            public uint Caps;
            public uint Caps2;
            public uint Caps3;
            public uint Caps4;
            public uint Reserved2;
        }

        /// <summary>
        /// DDS DX10 Header
        /// </summary>
        public struct DX10Header
        {
            public DXGIFormat Format;
            public TexDimension ResourceDimension;
            public TexMiscFlags MiscFlag; // see D3D11RESOURCEMISCFLAG
            public uint ArraySize;
            public uint MiscFlags2; // see DDSMISCFLAGS2
        }

        /// <summary>
        /// Texture Metadata
        /// </summary>
        public struct TexMetadata
        {
            #region Properties
            public long Width;
            public long Height;     // Should be 1 for 1D textures
            public long Depth;      // Should be 1 for 1D or 2D textures
            public long ArraySize;  // For cubemap, this is a multiple of 6
            public long MipLevels;
            public TexMiscFlags MiscFlags;
            public TexMiscFlags2 MiscFlags2;
            public DXGIFormat Format;
            public TexDimension Dimension;
            #endregion

            /// <summary>
            /// Creates a new Texture Metadata Structe
            /// </summary>
            public TexMetadata(long width, long height, long depth, long arraySize, long mipLevels, TexMiscFlags flags, TexMiscFlags2 flags2, DXGIFormat format, TexDimension dimension)
            {
                Width = width;
                Height = height;
                Depth = depth;
                ArraySize = arraySize;
                MipLevels = mipLevels;
                MiscFlags = flags;
                MiscFlags2 = flags2;
                Format = format;
                Dimension = dimension;
            }

            /// <summary>
            /// Checks Alpha Mode
            /// </summary>
            public bool IsPMAlpha()
            {
                return (TexAlphaMode)(MiscFlags2 & TexMiscFlags2.TEXMISC2ALPHAMODEMASK) == TexAlphaMode.PREMULTIPLIED;
            }

            public bool IsCubeMap()
            {
                return (MiscFlags & TexMiscFlags.TEXTURECUBE) == TexMiscFlags.TEXTURECUBE;
            }
        }
        #endregion

        #region HelperMethods
        /// <summary>
        /// Clamps Value to a range.
        /// </summary>
        /// <param name="value">Value to Clamp</param>
        /// <param name="max">Max value</param>
        /// <param name="min">Min value</param>
        /// <returns>Clamped Value</returns>
        private static T Clamp<T>(T value, T max, T min) where T : IComparable<T>
        {
            return value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
        }

        /// <summary>
        /// Converts a Struct to a Byte array
        /// </summary>
        private static byte[] StructToBytes<T>(T value)
        {
            // Size of Struct
            int length = Marshal.SizeOf<T>();
            // Destination
            byte[] destination = new byte[length];
            // Get Pointer
            IntPtr pointer = Marshal.AllocHGlobal(length);
            // Convert it
            Marshal.StructureToPtr(value, pointer, false);
            Marshal.Copy(pointer, destination, 0, length);
            Marshal.FreeHGlobal(pointer);
            // Done
            return destination;
        }

        /// <summary>
        /// Generates a FourCC Integer from Pixel Format Characters
        /// </summary>
        private static uint MakePixelFormatFourCC(char char1, char char2, char char3, char char4)
        {
            return Convert.ToByte(char1) | (uint)Convert.ToByte(char2) << 8 | (uint)Convert.ToByte(char3) << 16 | (uint)Convert.ToByte(char4) << 24;
        }

        /// <summary>
        /// Gets the Bits Per Pixel for the given format
        /// </summary>
        private static ulong BitsPerPixel(DXGIFormat format)
        {
            switch (format)
            {
                case DXGIFormat.R32G32B32A32TYPELESS:
                case DXGIFormat.R32G32B32A32FLOAT:
                case DXGIFormat.R32G32B32A32UINT:
                case DXGIFormat.R32G32B32A32SINT:
                    return 128;
                case DXGIFormat.R32G32B32TYPELESS:
                case DXGIFormat.R32G32B32FLOAT:
                case DXGIFormat.R32G32B32UINT:
                case DXGIFormat.R32G32B32SINT:
                    return 96;
                case DXGIFormat.R16G16B16A16TYPELESS:
                case DXGIFormat.R16G16B16A16FLOAT:
                case DXGIFormat.R16G16B16A16UNORM:
                case DXGIFormat.R16G16B16A16UINT:
                case DXGIFormat.R16G16B16A16SNORM:
                case DXGIFormat.R16G16B16A16SINT:
                case DXGIFormat.R32G32TYPELESS:
                case DXGIFormat.R32G32FLOAT:
                case DXGIFormat.R32G32UINT:
                case DXGIFormat.R32G32SINT:
                case DXGIFormat.R32G8X24TYPELESS:
                case DXGIFormat.D32FLOATS8X24UINT:
                case DXGIFormat.R32FLOATX8X24TYPELESS:
                case DXGIFormat.X32TYPELESSG8X24UINT:
                case DXGIFormat.Y416:
                case DXGIFormat.Y210:
                case DXGIFormat.Y216:
                    return 64;
                case DXGIFormat.R10G10B10A2TYPELESS:
                case DXGIFormat.R10G10B10A2UNORM:
                case DXGIFormat.R10G10B10A2UINT:
                case DXGIFormat.R11G11B10FLOAT:
                case DXGIFormat.R8G8B8A8TYPELESS:
                case DXGIFormat.R8G8B8A8UNORM:
                case DXGIFormat.R8G8B8A8UNORMSRGB:
                case DXGIFormat.R8G8B8A8UINT:
                case DXGIFormat.R8G8B8A8SNORM:
                case DXGIFormat.R8G8B8A8SINT:
                case DXGIFormat.R16G16TYPELESS:
                case DXGIFormat.R16G16FLOAT:
                case DXGIFormat.R16G16UNORM:
                case DXGIFormat.R16G16UINT:
                case DXGIFormat.R16G16SNORM:
                case DXGIFormat.R16G16SINT:
                case DXGIFormat.R32TYPELESS:
                case DXGIFormat.D32FLOAT:
                case DXGIFormat.R32FLOAT:
                case DXGIFormat.R32UINT:
                case DXGIFormat.R32SINT:
                case DXGIFormat.R24G8TYPELESS:
                case DXGIFormat.D24UNORMS8UINT:
                case DXGIFormat.R24UNORMX8TYPELESS:
                case DXGIFormat.X24TYPELESSG8UINT:
                case DXGIFormat.R9G9B9E5SHAREDEXP:
                case DXGIFormat.R8G8B8G8UNORM:
                case DXGIFormat.G8R8G8B8UNORM:
                case DXGIFormat.B8G8R8A8UNORM:
                case DXGIFormat.B8G8R8X8UNORM:
                case DXGIFormat.R10G10B10XRBIASA2UNORM:
                case DXGIFormat.B8G8R8A8TYPELESS:
                case DXGIFormat.B8G8R8A8UNORMSRGB:
                case DXGIFormat.B8G8R8X8TYPELESS:
                case DXGIFormat.B8G8R8X8UNORMSRGB:
                case DXGIFormat.AYUV:
                case DXGIFormat.Y410:
                case DXGIFormat.YUY2:
                    return 32;
                case DXGIFormat.P010:
                case DXGIFormat.P016:
                    return 24;
                case DXGIFormat.R8G8TYPELESS:
                case DXGIFormat.R8G8UNORM:
                case DXGIFormat.R8G8UINT:
                case DXGIFormat.R8G8SNORM:
                case DXGIFormat.R8G8SINT:
                case DXGIFormat.R16TYPELESS:
                case DXGIFormat.R16FLOAT:
                case DXGIFormat.D16UNORM:
                case DXGIFormat.R16UNORM:
                case DXGIFormat.R16UINT:
                case DXGIFormat.R16SNORM:
                case DXGIFormat.R16SINT:
                case DXGIFormat.B5G6R5UNORM:
                case DXGIFormat.B5G5R5A1UNORM:
                case DXGIFormat.A8P8:
                case DXGIFormat.B4G4R4A4UNORM:
                    return 16;
                case DXGIFormat.NV12:
                case DXGIFormat.OPAQUE420:
                case DXGIFormat.NV11:
                    return 12;
                case DXGIFormat.R8TYPELESS:
                case DXGIFormat.R8UNORM:
                case DXGIFormat.R8UINT:
                case DXGIFormat.R8SNORM:
                case DXGIFormat.R8SINT:
                case DXGIFormat.A8UNORM:
                case DXGIFormat.BC2TYPELESS:
                case DXGIFormat.BC2UNORM:
                case DXGIFormat.BC2UNORMSRGB:
                case DXGIFormat.BC3TYPELESS:
                case DXGIFormat.BC3UNORM:
                case DXGIFormat.BC3UNORMSRGB:
                case DXGIFormat.BC5TYPELESS:
                case DXGIFormat.BC5UNORM:
                case DXGIFormat.BC5SNORM:
                case DXGIFormat.BC6HTYPELESS:
                case DXGIFormat.BC6HUF16:
                case DXGIFormat.BC6HSF16:
                case DXGIFormat.BC7TYPELESS:
                case DXGIFormat.BC7UNORM:
                case DXGIFormat.BC7UNORMSRGB:    
                case DXGIFormat.AI44:
                case DXGIFormat.IA44:
                case DXGIFormat.P8:
                    return 8;
                case DXGIFormat.R1UNORM:
                    return 1;
                case DXGIFormat.BC1TYPELESS:
                case DXGIFormat.BC1UNORM:
                case DXGIFormat.BC1UNORMSRGB:
                case DXGIFormat.BC4TYPELESS:
                case DXGIFormat.BC4UNORM:
                case DXGIFormat.BC4SNORM:
                    return 4;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Computes Row and Slice Pitch
        /// </summary>
        private static void ComputePitch(DXGIFormat format, uint width, uint height, out ulong rowPitch, out ulong slicePitch, CPFLAGS flags)
        {
            switch (format)
            {
                case DXGIFormat.BC1TYPELESS:
                case DXGIFormat.BC1UNORM:
                case DXGIFormat.BC1UNORMSRGB:
                case DXGIFormat.BC4TYPELESS:
                case DXGIFormat.BC4UNORM:
                case DXGIFormat.BC4SNORM:
                    {
                        if (flags.HasFlag(CPFLAGS.BADDXTNTAILS))
                        {
                            var nbw = width >> 2;
                            var nbh = height >> 2;
                            rowPitch = Clamp((ulong) nbw * 8u, ulong.MaxValue, 1u);
                            slicePitch = Clamp(rowPitch * nbh, ulong.MaxValue, 1u);
                        }
                        else
                        {
                            var nbw = Clamp(((ulong) width + 3u) / 4u, ulong.MaxValue, 1u);
                            var nbh = Clamp(((ulong) height + 3u) / 4u, ulong.MaxValue, 1u);
                            rowPitch = nbw * 8u;
                            slicePitch = rowPitch * nbh;
                        }
                    }
                    break;
                case DXGIFormat.BC2TYPELESS:
                case DXGIFormat.BC2UNORM:
                case DXGIFormat.BC2UNORMSRGB:
                case DXGIFormat.BC3TYPELESS:
                case DXGIFormat.BC3UNORM:
                case DXGIFormat.BC3UNORMSRGB:
                case DXGIFormat.BC5TYPELESS:
                case DXGIFormat.BC5UNORM:
                case DXGIFormat.BC5SNORM:
                case DXGIFormat.BC6HTYPELESS:
                case DXGIFormat.BC6HUF16:
                case DXGIFormat.BC6HSF16:
                case DXGIFormat.BC7TYPELESS:
                case DXGIFormat.BC7UNORM:
                case DXGIFormat.BC7UNORMSRGB:
                    {
                        if (flags.HasFlag(CPFLAGS.BADDXTNTAILS))
                        {
                            var nbw = width >> 2;
                            var nbh = height >> 2;
                            rowPitch = Clamp((ulong) nbw * 16u, ulong.MaxValue, 1u);
                            slicePitch = Clamp(rowPitch * nbh, ulong.MaxValue, 1u);
                        }
                        else
                        {
                            var nbw = Clamp((width + 3) / 4, ulong.MaxValue, 1u);
                            var nbh = Clamp((height + 3) / 4, ulong.MaxValue, 1u);
                            rowPitch = nbw * 16u;
                            slicePitch = rowPitch * nbh;
                        }
                    }
                    break;
                case DXGIFormat.R8G8B8G8UNORM:
                case DXGIFormat.G8R8G8B8UNORM:
                case DXGIFormat.YUY2:
                    rowPitch = ((width + 1) >> 1) * 4;
                    slicePitch = rowPitch * height;
                    break;
                case DXGIFormat.Y210:
                case DXGIFormat.Y216:
                    rowPitch = ((width + 1) >> 1) * 8;
                    slicePitch = rowPitch * height;
                    break;

                case DXGIFormat.NV12:
                case DXGIFormat.OPAQUE420:
                    rowPitch = ((width + 1) >> 1) * 2;
                    slicePitch = rowPitch * (height + ((height + 1) >> 1));
                    break;

                case DXGIFormat.P010:
                case DXGIFormat.P016:
                    rowPitch = ((width + 1) >> 1) * 4;
                    slicePitch = rowPitch * (height + ((height + 1) >> 1));
                    break;
                case DXGIFormat.NV11:
                    rowPitch = ((width + 3) >> 2) * 4;
                    slicePitch = rowPitch * height * 2;
                    break;
                default:
                    {
                        ulong bpp;

                        if (flags.HasFlag(CPFLAGS.BPP24))
                            bpp = 24;
                        else if (flags.HasFlag(CPFLAGS.BPP16))
                            bpp = 16;
                        else if (flags.HasFlag(CPFLAGS.BPP8))
                            bpp = 8;
                        else
                            bpp = BitsPerPixel(format);

                        if (flags.HasFlag(CPFLAGS.LEGACYDWORD | CPFLAGS.PARAGRAPH | CPFLAGS.YMM | CPFLAGS.ZMM | CPFLAGS.PAGE4K))
                        {
                            if (flags.HasFlag(CPFLAGS.PAGE4K))
                            {
                                rowPitch = (width * bpp + 32767u) / 32768u * 4096u;
                                slicePitch = rowPitch * height;
                            }
                            else if (flags.HasFlag(CPFLAGS.ZMM))
                            {
                                rowPitch = (width * bpp + 511u) / 512u * 64u;
                                slicePitch = rowPitch * height;
                            }
                            else if (flags.HasFlag(CPFLAGS.YMM))
                            {
                                rowPitch = (width * bpp + 255u) / 256u * 32u;
                                slicePitch = rowPitch * height;
                            }
                            else if (flags.HasFlag(CPFLAGS.PARAGRAPH))
                            {
                                rowPitch = (width * bpp + 127u) / 128u * 16u;
                                slicePitch = rowPitch * height;
                            }
                            else // DWORD alignment
                            {
                                // Special computation for some incorrectly created DDS files based on
                                // legacy DirectDraw assumptions about pitch alignment
                                rowPitch = (width * bpp + 31u) / 32u * sizeof(uint);
                                slicePitch = rowPitch * height;
                            }
                        }
                        else
                        {
                            // Default byte alignment
                            rowPitch = (width * bpp + 7u) / 8u;
                            slicePitch = rowPitch * height;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Checks is the given format compressed
        /// </summary>
        private static bool IsCompressed(DXGIFormat format)
        {
            switch (format)
            {
                case DXGIFormat.BC1TYPELESS:
                case DXGIFormat.BC1UNORM:
                case DXGIFormat.BC1UNORMSRGB:
                case DXGIFormat.BC2TYPELESS:
                case DXGIFormat.BC2UNORM:
                case DXGIFormat.BC2UNORMSRGB:
                case DXGIFormat.BC3TYPELESS:
                case DXGIFormat.BC3UNORM:
                case DXGIFormat.BC3UNORMSRGB:
                case DXGIFormat.BC4TYPELESS:
                case DXGIFormat.BC4UNORM:
                case DXGIFormat.BC4SNORM:
                case DXGIFormat.BC5TYPELESS:
                case DXGIFormat.BC5UNORM:
                case DXGIFormat.BC5SNORM:
                case DXGIFormat.BC6HTYPELESS:
                case DXGIFormat.BC6HUF16:
                case DXGIFormat.BC6HSF16:
                case DXGIFormat.BC7TYPELESS:
                case DXGIFormat.BC7UNORM:
                case DXGIFormat.BC7UNORMSRGB:
                    return true;

                default:
                    return false;
            }
        }
        #endregion

        #region MainMethods
        /// <summary>
        /// Encodes the DDS Header and if DX10, the DX10 Header
        /// </summary>
        /// <param name="header">DDS Header</param>
        /// <param name="dx10Header">DX10 Header</param>
        /// <returns>Resulting DDS File Header in bytes</returns>
        public static byte[] EncodeDDSHeader(DDSHeader header, DX10Header dx10Header)
        {
            // Create stream
            using (var output = new BinaryWriter(new MemoryStream()))
            {
                // Write DDS Magic
                output.Write(DDSHeader.DDSMagic);
                // Write Header
                output.Write(StructToBytes(header));
                // Check for DX10 Header
                if (header.PixelFormat.FourCC == PixelFormats.DX10.FourCC)
                    // Write Header
                    output.Write(StructToBytes(dx10Header));
                // Done
                return ((MemoryStream)(output.BaseStream)).ToArray();
            }
        }

        /// <summary>
        /// Generates DirectXTex Meta Data
        /// </summary>
        /// <param name="width">Image Width</param>
        /// <param name="height">Image Height</param>
        /// <param name="mipMapLevels">Number of Mip Maps</param>
        /// <param name="format">Compression Format</param>
        /// <param name="isCubeMap">Whether or not this is a cube map</param>
        /// <returns>Resulting TexMetaData Object</returns>
        public static TexMetadata GenerateMetadata(int width, int height, int mipMapLevels, DXGIFormat format, bool isCubeMap)
        {
            // Create Texture MetaData
            return new TexMetadata(
                width,
                height,
                1,
                isCubeMap ? 6 : 1,
                mipMapLevels,
                isCubeMap ? TexMiscFlags.TEXTURECUBE : 0,
                0,
                format,
                TexDimension.TEXTURE2D
                );
        }

        private static readonly uint[] _elevenUInts = Enumerable.Repeat((uint)0, 11).ToArray();
        
        /// <summary>
        /// Generates a DDS Header, and if requires, a DX10 Header
        /// </summary>
        /// <param name="metaData">Meta Data</param>
        /// <param name="flags">Flags</param>
        /// <param name="header">DDS Header Output</param>
        /// <param name="dx10Header">DX10 Header Output</param>
        public static void GenerateDDSHeader(TexMetadata metaData, DDSFlags flags, out DDSHeader header, out DX10Header dx10Header)
        {
            // Check array size
            if (metaData.ArraySize > 1)
                // Check if we have an array and whether we're cube maps/non-2D
                if (metaData.ArraySize != 6 || metaData.Dimension != TexDimension.TEXTURE2D || !metaData.IsCubeMap())
                    // Texture1D arrays, Texture2D arrays, and Cubemap arrays must be stored using 'DX10' extended header
                    flags |= DDSFlags.FORCEDX10EXT;

            // Check for DX10 Ext
            if (flags.HasFlag(DDSFlags.FORCEDX10EXTMISC2))
                flags |= DDSFlags.FORCEDX10EXT;
            
            // Create DDS Header
            header = new DDSHeader
            {
                // Set Data
                Size = (uint) Marshal.SizeOf<DDSHeader>(),
                Flags = DDSHeader.HeaderFlags.TEXTURE,
                Height = 0,
                Width = 0,
                PitchOrLinearSize = 0,
                Depth = 0,
                MipMapCount = 0,
                Reserved1 = _elevenUInts,
                Caps = (uint) DDSHeader.SurfaceFlags.TEXTURE,
                Caps2 = 0,
                Caps3 = 0,
                Caps4 = 0,
                Reserved2 = 0,
                PixelFormat = new DDSHeader.DDSPixelFormat(),

            };
            
            // Create DX10 Header
            dx10Header = new DX10Header
            {
                Format = 0,
                ResourceDimension = (TexDimension) 0,
                MiscFlag = (TexMiscFlags) 0,
                ArraySize = 0,
                MiscFlags2 = 0,
            };
            
            // Switch format
            header.PixelFormat = GetPixelFormat(metaData);
            // Check for mips
            if (metaData.MipLevels > 0)
            {
                // Set flag
                header.Flags |= DDSHeader.HeaderFlags.MIPMAP;
                // Check size
                if (metaData.MipLevels > UInt16.MaxValue)
                    throw new ArgumentException(String.Format("Too many mipmaps: {0}. Max: {1}", metaData.MipLevels, UInt16.MaxValue));
                // Set
                header.MipMapCount = (uint)metaData.MipLevels;
                // Check count
                if (header.MipMapCount > 1)
                    header.Caps |= (uint)DDSHeader.SurfaceFlags.MIPMAP;
            }

            // Switch Dimension
            switch (metaData.Dimension)
            {
                case TexDimension.TEXTURE1D:
                    {
                        // Check size
                        if (metaData.Width > Int32.MaxValue)
                            throw new ArgumentException(String.Format("Image Width too large: {0}. Max: {1}", metaData.Width, Int32.MaxValue));
                        // Set
                        header.Width = (uint)metaData.Width;
                        header.Height = header.Depth = 1;
                        // Check size
                        break;
                    }
                case TexDimension.TEXTURE2D:
                    {
                        // Check size
                        if (metaData.Width > Int32.MaxValue || metaData.Height > Int32.MaxValue)
                            throw new ArgumentException(String.Format("Image Width and/or Height too large: {0}x{1}. Max: {2}",
                                metaData.Width,
                                metaData.Height,
                                Int32.MaxValue));
                        // Set
                        header.Width = (uint)metaData.Width;
                        header.Height = (uint)metaData.Height;
                        header.Depth = 1;
                        // Check size
                        break;
                    }
                case TexDimension.TEXTURE3D:
                    {
                        // Check size
                        if (metaData.Width > Int32.MaxValue || metaData.Height > Int32.MaxValue)
                            throw new ArgumentException(String.Format("Image Width and/or Height too large: {0}x{1}. Max: {2}",
                                metaData.Width,
                                metaData.Height,
                                Int32.MaxValue));
                        // Check size
                        if (metaData.Depth > UInt16.MaxValue)
                            throw new ArgumentException(String.Format("Image Depth too large: {0}. Max: {1}", metaData.Depth, UInt16.MaxValue));
                        // Set
                        header.Flags |= DDSHeader.HeaderFlags.VOLUME;
                        header.Caps2 |= 0x00200000;
                        header.Width = (uint)metaData.Width;
                        header.Height = (uint)metaData.Height;
                        header.Depth = (uint)metaData.Depth;
                        // Check size
                        break;
                    }
                default:
                    throw new ArgumentException("Invalid Texture Dimension.");

            }
            // Calculate the Pitch
            ComputePitch(metaData.Format, (uint) metaData.Width, (uint) metaData.Height, out var rowPitch, out var slicePitch, CPFLAGS.NONE);
            // Validate results
            if (slicePitch > UInt32.MaxValue || rowPitch > UInt32.MaxValue)
                throw new ArgumentException("Failed to calculate row and/or slice pitch, values returned were too large");
            // Check is it compressed
            if (IsCompressed(metaData.Format))
            {
                header.Flags |= DDSHeader.HeaderFlags.LINEARSIZE;
                header.PitchOrLinearSize = (uint)slicePitch;
            }
            else
            {
                header.Flags |= DDSHeader.HeaderFlags.PITCH;
                header.PitchOrLinearSize = (uint)rowPitch;
            }

            // Check for do we need to create the DX10 Header
            if (HasDx10Header(header.PixelFormat))
            {
                // Check size
                if (metaData.ArraySize > UInt16.MaxValue)
                    throw new ArgumentException(String.Format("Array Size too large: {0}. Max: {1}", metaData.ArraySize, UInt16.MaxValue));
                // Set Pixel format
                header.PixelFormat = PixelFormats.DX10;
                // Set Data
                dx10Header.Format = metaData.Format;
                dx10Header.ResourceDimension = metaData.Dimension;
                dx10Header.MiscFlag = metaData.MiscFlags & ~TexMiscFlags.TEXTURECUBE;
                dx10Header.ArraySize = (uint)metaData.ArraySize;
                // Check for Cube Maps
                if (metaData.MiscFlags.HasFlag(TexMiscFlags.TEXTURECUBE))
                {
                    // Check array size, must be a multiple of 6 for cube maps
                    if ((metaData.ArraySize % 6) != 0)
                        throw new ArgumentException("Array size must be a multiple of 6");
                    // Set Flag
                    dx10Header.MiscFlag |= TexMiscFlags.TEXTURECUBE;
                    dx10Header.ArraySize /= 6;
                }
                // Check for mist flags
                if (flags.HasFlag(DDSFlags.FORCEDX10EXTMISC2))
                    // This was formerly 'reserved'. D3DX10 and D3DX11 will fail if this value is anything other than 0
                    dx10Header.MiscFlags2 = (uint)metaData.MiscFlags2;
            }
        }

        public static bool HasDx10Header(DDSHeader.DDSPixelFormat pixelFormat) => pixelFormat.Size == 0;

        public static DDSHeader.DDSPixelFormat GetPixelFormat(TexMetadata metaData) =>
            metaData.Format switch
            {
                DXGIFormat.R8G8B8A8UNORM => PixelFormats.A8B8G8R8,
                DXGIFormat.R16G16UNORM   => PixelFormats.G16R16,
                DXGIFormat.R8G8UNORM     => PixelFormats.A8L8,
                DXGIFormat.R16UNORM      => PixelFormats.L16,
                DXGIFormat.R8UNORM       => PixelFormats.L8,
                DXGIFormat.A8UNORM       => PixelFormats.A8,
                DXGIFormat.R8G8B8G8UNORM => PixelFormats.R8G8B8G8,
                DXGIFormat.G8R8G8B8UNORM => PixelFormats.G8R8G8B8,
                DXGIFormat.BC1UNORM      => PixelFormats.DXT1,
                DXGIFormat.BC2UNORM      => metaData.IsPMAlpha() ? (PixelFormats.DXT2) : (PixelFormats.DXT3),
                DXGIFormat.BC3UNORM      => metaData.IsPMAlpha() ? (PixelFormats.DXT4) : (PixelFormats.DXT5),
                DXGIFormat.BC4UNORM      => PixelFormats.BC4UNORM,
                DXGIFormat.BC4SNORM      => PixelFormats.BC4SNORM,
                DXGIFormat.BC5UNORM      => PixelFormats.BC5UNORM,
                DXGIFormat.BC5SNORM      => PixelFormats.BC5SNORM,
                DXGIFormat.B5G6R5UNORM   => PixelFormats.R5G6B5,
                DXGIFormat.B5G5R5A1UNORM => PixelFormats.A1R5G5B5,
                DXGIFormat.R8G8SNORM     => PixelFormats.V8U8,
                DXGIFormat.R8G8B8A8SNORM => PixelFormats.Q8W8V8U8,
                DXGIFormat.R16G16SNORM   => PixelFormats.V16U16,
                DXGIFormat.B8G8R8A8UNORM => PixelFormats.A8R8G8B8,
                DXGIFormat.B8G8R8X8UNORM => PixelFormats.X8R8G8B8,
                DXGIFormat.B4G4R4A4UNORM => PixelFormats.A4R4G4B4,
                DXGIFormat.YUY2          => PixelFormats.YUY2,
                // Legacy D3DX formats using D3DFMT enum value as FourCC
                DXGIFormat.R32G32B32A32FLOAT => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 116 /* D3DFMTA32B32G32R32F */ },
                DXGIFormat.R16G16B16A16FLOAT => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 113 /* D3DFMTA16B16G16R16F */ },
                DXGIFormat.R16G16B16A16UNORM => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 36 /* D3DFMTA16B16G16R16 */ },
                DXGIFormat.R16G16B16A16SNORM => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 110 /* D3DFMTQ16W16V16U16 */ },
                DXGIFormat.R32G32FLOAT       => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 115 /* D3DFMTG32R32F */ },
                DXGIFormat.R16G16FLOAT       => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 112 /* D3DFMTG16R16F */ },
                DXGIFormat.R32FLOAT          => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 114 /* D3DFMTR32F */ },
                DXGIFormat.R16FLOAT          => new DDSHeader.DDSPixelFormat { Flags = PixelFormats.DDSFOURCC, FourCC = 111 /* D3DFMTR16F */ },
                _                            => new DDSHeader.DDSPixelFormat()
            };

        #endregion
    }
}
