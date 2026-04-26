using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using BCnEncoder.Shared.ImageFiles;
using Shipwreck.Phash;
using Shipwreck.Phash.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Wabbajack.Common;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Hashing.PHash;

public class CrossPlatformImageLoader : IImageLoader
{
    public async ValueTask<ImageState> Load(AbsolutePath path)
    {
        await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await Load(fs);
    }

    public async ValueTask<ImageState> Load(Stream stream)
    {
        var decoder = new BcDecoder();
        var ddsFile = DdsFile.Load(stream);
        var data = await decoder.DecodeToImageRgba32Async(ddsFile);

        var format = ddsFile.dx10Header.dxgiFormat == DxgiFormat.DxgiFormatUnknown
            ? ddsFile.header.ddsPixelFormat.DxgiFormat
            : ddsFile.dx10Header.dxgiFormat;

        var state = new ImageState
        {
            Width = data.Width,
            Height = data.Height,
            MipLevels = (int)ddsFile.header.dwMipMapCount,
            Format = (DXGI_FORMAT) format
        };

        data.Mutate(x => x.Resize(512, 512, KnownResamplers.Welch).Grayscale(GrayscaleMode.Bt601));

        var hash = ImagePhash.ComputeDigest(new ImageBitmap(data));
        state.PerceptualHash = new DTOs.Texture.PHash(hash.Coefficients);
        return state;
    }

    public static float ComputeDifference(DTOs.Texture.PHash a, DTOs.Texture.PHash b)
    {
        return ImagePhash.GetCrossCorrelation(
            new Digest {Coefficients = a.Data},
            new Digest {Coefficients = b.Data});
    }
    
    public async Task Recompress(AbsolutePath input, int width, int height, int mipMaps, DXGI_FORMAT format,
        AbsolutePath output,
        CancellationToken token)
    {
        var inData = await input.ReadAllBytesAsync(token);
        await using var outStream = output.Open(FileMode.Create, FileAccess.Write);
        await Recompress(new MemoryStream(inData), width, height, mipMaps, format, outStream, token);
    }

    public async Task Recompress(Stream input, int width, int height, int mipMaps, DXGI_FORMAT format, Stream output,
        CancellationToken token, bool leaveOpen = false)
    {
        var decoder = new BcDecoder();
        var ddsFile = DdsFile.Load(input);

        if (!leaveOpen) await input.DisposeAsync();

        var faces = new List<Image<Rgba32>>();

        var origFormat = ddsFile.dx10Header.dxgiFormat == DxgiFormat.DxgiFormatUnknown
            ? ddsFile.header.ddsPixelFormat.DxgiFormat
            : ddsFile.dx10Header.dxgiFormat;

        foreach (var face in ddsFile.Faces)
        {
            var data = await decoder.DecodeRawToImageRgba32Async(face.MipMaps[0].Data,
                (int)face.Width, (int)face.Height, ToCompressionFormat((DXGI_FORMAT)origFormat), token);

            data.Mutate(x => x.Resize(width, height, KnownResamplers.Welch));
            faces.Add(data);
        }

        var encoder = new BcEncoder
        {
            OutputOptions =
            {
                Quality = CompressionQuality.Balanced,
                GenerateMipMaps = true,
                Format = ToCompressionFormat(format),
                FileFormat = OutputFileFormat.Dds,
                MaxMipMapLevel = mipMaps != 0 ? mipMaps : -1
            }
        };

        switch (faces.Count)
        {
            case 1:
                (await encoder.EncodeToDdsAsync(faces[0], token)).Write(output);
                break;
            case 6:
                (await encoder.EncodeCubeMapToDdsAsync(faces[0], faces[1], faces[2], faces[3], faces[4], faces[5], token))
                    .Write(output);
                break;
            default:
                throw new NotImplementedException($"Can't encode dds with {faces.Count} faces");
        }

        if (!leaveOpen)
            await output.DisposeAsync();
    }
    
    public static CompressionFormat ToCompressionFormat(DXGI_FORMAT dx)
    {
        return dx switch
        {
            DXGI_FORMAT.BC1_UNORM => CompressionFormat.Bc1,
            DXGI_FORMAT.BC2_UNORM => CompressionFormat.Bc2,
            DXGI_FORMAT.BC3_UNORM => CompressionFormat.Bc3,
            DXGI_FORMAT.BC4_UNORM => CompressionFormat.Bc4,
            DXGI_FORMAT.BC5_UNORM => CompressionFormat.Bc5,
            DXGI_FORMAT.BC7_UNORM => CompressionFormat.Bc7,
            DXGI_FORMAT.B8G8R8A8_UNORM => CompressionFormat.Bgra,
            DXGI_FORMAT.R8G8B8A8_UNORM => CompressionFormat.Rgba,
            _ => throw new Exception($"Cannot re-encode texture with {dx} format, encoding not supported")
        };
    }

    public class ImageBitmap : IByteImage
    {
        private readonly Image<Rgba32> _image;

        public ImageBitmap(Image<Rgba32> image)
        {
            _image = image;
        }

        public int Width => _image.Width;
        public int Height => _image.Height;

        public byte this[int x, int y] => _image[x, y].R;
    }
}