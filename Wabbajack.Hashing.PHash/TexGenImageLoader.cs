using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shipwreck.Phash;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TexGen;
using TexGen.Gpu;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using TexImage = TexGen.Image;

namespace Wabbajack.Hashing.PHash;

/// <summary>
///     Reads and recompresses textures in process with texgen, a C# port of texconv that runs resize, mips and
///     block compression on the GPU (CUDA or OpenCL) and falls back to DirectXTex's CPU codec without one. Unlike
///     <see cref="TexConvImageLoader" /> it needs no Windows executable, so it is the loader on Linux and macOS.
///     <para>
///         Output uses the legacy DDS header whenever the format fits one, as texconv does, because Oblivion,
///         Fallout 3, New Vegas and Skyrim LE cannot read the DX10 header.
///     </para>
/// </summary>
public class TexGenImageLoader : IImageLoader
{
    private readonly ILogger<TexGenImageLoader> _logger;
    private readonly Lazy<GpuDevice> _device;

    public TexGenImageLoader() : this(NullLogger<TexGenImageLoader>.Instance)
    {
    }

    public TexGenImageLoader(ILogger<TexGenImageLoader> logger)
    {
        _logger = logger;
        _device = new Lazy<GpuDevice>(CreateDevice, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private GpuDevice CreateDevice()
    {
        try
        {
            var device = GpuDevice.Create();
            _logger.LogInformation("Textures are converted on {Device}", device.Description);
            return device;
        }
        catch (Exception ex)
        {
            // A broken CUDA or OpenCL driver should cost speed, not the install.
            _logger.LogWarning(ex, "No usable GPU for texture conversion, using the CPU");
            return GpuDevice.Create(GpuBackend.Cpu);
        }
    }

    public async ValueTask<ImageState> Load(AbsolutePath path)
    {
        return Load(await path.ReadAllBytesAsync());
    }

    public async ValueTask<ImageState> Load(Stream stream)
    {
        // The caller rewinds and goes on using the stream, so it is read, never disposed.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return Load(ms.ToArray());
    }

    private static ImageState Load(byte[] data)
    {
        var scratch = Dds.Read(data);
        var meta = scratch.Metadata;
        return new ImageState
        {
            Width = meta.Width,
            Height = meta.Height,
            MipLevels = meta.MipLevels,
            Format = (DXGI_FORMAT) (uint) meta.Format,
            PerceptualHash = PerceptualHash(scratch.BaseImage)
        };
    }

    /// <summary>The same digest <see cref="CrossPlatformImageLoader" /> computes, from texgen's decoders.</summary>
    private static DTOs.Texture.PHash PerceptualHash(TexImage baseImage)
    {
        var rgba = ToRgba8(ImageIO.DecodeToWorking(baseImage));
        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
            rgba.Pixels.AsSpan(0, rgba.Width * rgba.Height * 4), rgba.Width, rgba.Height);
        image.Mutate(x => x.Resize(512, 512, KnownResamplers.Welch).Grayscale(GrayscaleMode.Bt601));
        var digest = ImagePhash.ComputeDigest(new CrossPlatformImageLoader.ImageBitmap(image));
        return new DTOs.Texture.PHash(digest.Coefficients);
    }

    /// <summary>HDR sources decode to float; everything 8-bit wants RGBA8.</summary>
    private static TexImage ToRgba8(TexImage working)
    {
        return working.IsRgba8 ? working : PixelConvert.Encode(working, DxgiFormat.R8G8B8A8_UNORM);
    }

    public async Task Recompress(AbsolutePath input, int width, int height, int mipMaps, DXGI_FORMAT format,
        AbsolutePath output, CancellationToken token)
    {
        var data = await input.ReadAllBytesAsync(token);
        var result = Recompress(data, width, height, mipMaps, format, token);
        await output.WriteAllBytesAsync(result, token);
    }

    public async Task Recompress(Stream input, int width, int height, int mipMaps, DXGI_FORMAT format,
        Stream output, CancellationToken token, bool leaveOpen = false)
    {
        byte[] data;
        using (var ms = new MemoryStream())
        {
            await input.CopyToAsync(ms, token);
            data = ms.ToArray();
        }

        if (!leaveOpen) await input.DisposeAsync();

        var result = Recompress(data, width, height, mipMaps, format, token);
        await output.WriteAsync(result, token);

        if (!leaveOpen) await output.DisposeAsync();
    }

    /// <summary>
    ///     Resize every face to <paramref name="width" /> x <paramref name="height" />, build
    ///     <paramref name="mipMaps" /> levels (0 for as many as the source has, as texconv reads it) and encode them
    ///     as <paramref name="format" />. A cubemap stays a cubemap.
    ///     <para>
    ///         Colour and alpha are filtered separately. texconv as Wabbajack calls it does not, and darkens colour
    ///         wherever alpha is low, which on a Skyrim normal map (specular in alpha) bends the normals.
    ///     </para>
    /// </summary>
    private byte[] Recompress(byte[] data, int width, int height, int mipMaps, DXGI_FORMAT format,
        CancellationToken token)
    {
        var source = Dds.Read(data);
        var meta = source.Metadata;
        var target = (DxgiFormat) (uint) format;
        var options = new ConvertOptions
        {
            Format = target,
            Width = width,
            Height = height,
            // texconv with no mip count keeps the source's, and builds a full chain only for a source without mips.
            MipLevels = mipMaps == 0 && meta.MipLevels > 1 ? meta.MipLevels : mipMaps
        };
        var floatTarget = target is DxgiFormat.BC6H_UF16 or DxgiFormat.BC6H_SF16
            || PixelConvert.IsFloatFormat(target);

        var images = new List<TexImage>();
        TexMetadata? converted = null;
        for (var slice = 0; slice < meta.ArraySize; slice++)
        {
            token.ThrowIfCancellationRequested();
            var working = ImageIO.DecodeToWorking(source.Images[slice * meta.MipLevels]);
            if (!floatTarget) working = ToRgba8(working);
            var result = Converter.Convert(working, options, _device.Value);
            converted ??= result.Metadata;
            images.AddRange(result.Images);
        }

        var outMeta = converted! with
        {
            ArraySize = meta.ArraySize,
            MiscFlags = meta.MiscFlags & TexMetadata.TexMiscTextureCube
        };
        return Dds.Write(ScratchImage.FromImages(outMeta, images), header: DdsHeader.LegacyWhenPossible);
    }
}
