using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DirectXTexNet;
using Shipwreck.Phash;
using Shipwreck.Phash.Imaging;
using Wabbajack.Common;

namespace Wabbajack.ImageHashing
{
    public class DDSImage
    {
        private DDSImage(ScratchImage img, TexMetadata metadata)
        {
            _image = img;
            _metaData = metadata;
        }

        private static Extension DDSExtension = new(".dds");
        private ScratchImage _image;
        private TexMetadata _metaData;

        public static DDSImage FromFile(AbsolutePath file)
        {
            if (file.Extension != DDSExtension)
                throw new Exception("File does not end in DDS");

            var img = TexHelper.Instance.LoadFromDDSFile(file.ToString(), DDS_FLAGS.NONE);
            
            return new DDSImage(img, img.GetMetadata());
        }
        
        public static DDSImage FromDDSMemory(byte[] data)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    var img = TexHelper.Instance.LoadFromDDSMemory((IntPtr)ptr, data.Length, DDS_FLAGS.NONE);
                    return new DDSImage(img, img.GetMetadata());
                }
            }
        }
        
        public static DDSImage FromTGAMemory(byte[] data)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    var img = TexHelper.Instance.LoadFromTGAMemory((IntPtr)ptr, data.Length);
                    return new DDSImage(img, img.GetMetadata());
                }
            }
        }

        public void Dispose()
        {
            if (!_image.IsDisposed) 
                _image.Dispose();
        }

        public int Width => _metaData.Width;
        public int Height => _metaData.Height;

        public void Resize(int width, int height)
        {

        }

        private static HashSet<DXGI_FORMAT> CompressedTypes = new HashSet<DXGI_FORMAT>()
        {
            DXGI_FORMAT.BC1_TYPELESS,
            DXGI_FORMAT.BC1_UNORM,
            DXGI_FORMAT.BC1_UNORM_SRGB,
            DXGI_FORMAT.BC2_TYPELESS,
            DXGI_FORMAT.BC2_UNORM,
            DXGI_FORMAT.BC2_UNORM_SRGB,
            DXGI_FORMAT.BC3_TYPELESS,
            DXGI_FORMAT.BC3_UNORM,
            DXGI_FORMAT.BC3_UNORM_SRGB,
            DXGI_FORMAT.BC4_TYPELESS,
            DXGI_FORMAT.BC4_UNORM,
            DXGI_FORMAT.BC4_SNORM,
            DXGI_FORMAT.BC5_TYPELESS,
            DXGI_FORMAT.BC5_UNORM,
            DXGI_FORMAT.BC5_SNORM,
            DXGI_FORMAT.BC6H_TYPELESS,
            DXGI_FORMAT.BC6H_UF16,
            DXGI_FORMAT.BC6H_SF16,
            DXGI_FORMAT.BC7_TYPELESS,
            DXGI_FORMAT.BC7_UNORM,
            DXGI_FORMAT.BC7_UNORM_SRGB,
        };

        public PHash PerceptionHash()
        {
            ScratchImage? resized = default;
            try
            {
                if (CompressedTypes.Contains(_metaData.Format))
                {
                    using var decompressed = _image.Decompress(DXGI_FORMAT.UNKNOWN);
                    resized = decompressed.Resize(512, 512, TEX_FILTER_FLAGS.DEFAULT);
                }
                else
                {
                    resized = _image.Resize(512, 512, TEX_FILTER_FLAGS.DEFAULT);
                }

                var data = new List<(int, int)>();
                var image = new byte[512 * 512];

                unsafe void EvaluatePixels(IntPtr pixels, IntPtr width, IntPtr line)
                {
                    float* ptr = (float*)pixels.ToPointer();

                    int widthV = width.ToInt32();
                    if (widthV != 512) return;

                    var y = line.ToInt32();
                    data.Add((widthV, y));

                    for (int i = 0; i < widthV; i++)
                    {
                        var r = ptr[0] * 0.229f;
                        var g = ptr[1] * 0.587f;
                        var b = ptr[2] * 0.114f;

                        var combined = (r + g + b) * 255.0f;

                        image[(y * widthV) + i] = (byte)combined;
                        ptr += 4;
                    }

                }

                resized.EvaluateImage(EvaluatePixels);

                var digest = ImagePhash.ComputeDigest(new ByteImage(512, 512, image));
                return PHash.FromDigest(digest);
            }
            finally
            {
                resized?.Dispose();
            }
        }
        
    }
}
