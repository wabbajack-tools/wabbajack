using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Wabbajack.App.Avalonia.Models;
using Wabbajack.DTOs;

namespace Wabbajack.App.Avalonia.Util;

public static class UIUtils
{
    public static void OpenWebsite(Uri url)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start {url.ToString().Replace(" ", "%20")}")
        {
            CreateNoWindow = true,
        });
    }

    public static void OpenWebsite(string url)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start {url}")
        {
            CreateNoWindow = true,
        });
    }

    /// <summary>
    /// Downloads an image from the URL stream and returns an Avalonia <see cref="Bitmap"/>.
    /// Uses <paramref name="icm"/> for disk + memory caching.
    /// </summary>
    public static IObservable<Bitmap?> DownloadBitmapImage(
        this IObservable<string> obs,
        Action<Exception> exceptionHandler,
        LoadingLock loadingLock,
        HttpClient client,
        ImageCacheManager icm)
    {
        const int MaxConcurrent = 8;

        return obs
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(url => Observable.FromAsync(async () =>
            {
                using var ll = loadingLock.WithLoading();
                try
                {
                    var (cached, cachedBytes) = await icm.Get(url);
                    if (cached && cachedBytes != null)
                        return BitmapFromBytes(cachedBytes);

                    await using var net = await client.GetStreamAsync(url);

                    using var sharpImg = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Bgra32>(net);
                    const int targetPx = 512;
                    if (sharpImg.Width > targetPx || sharpImg.Height > targetPx)
                    {
                        var scale = Math.Min((float)targetPx / sharpImg.Width, (float)targetPx / sharpImg.Height);
                        sharpImg.Mutate(x => x.Resize((int)(sharpImg.Width * scale), (int)(sharpImg.Height * scale)));
                    }

                    using var pngStream = new MemoryStream(capacity: 64 * 1024);
                    var fastPng = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.NoCompression,
                        FilterMethod = PngFilterMethod.None,
                        BitDepth = PngBitDepth.Bit8,
                        ColorType = PngColorType.RgbWithAlpha,
                    };

                    try
                    {
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Some images have buggy ICC/EXIF profiles — strip metadata and retry
                        sharpImg.Metadata.IccProfile = null;
                        sharpImg.Metadata.ExifProfile = null;
                        sharpImg.Metadata.XmpProfile = null;
                        foreach (var frame in sharpImg.Frames)
                        {
                            frame.Metadata.IccProfile = null;
                            frame.Metadata.ExifProfile = null;
                            frame.Metadata.XmpProfile = null;
                        }
                        pngStream.SetLength(0);
                        pngStream.Position = 0;
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }

                    var bytes = pngStream.ToArray();
                    await icm.Add(url, bytes);
                    return BitmapFromBytes(bytes);
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return default;
                }
            }))
            .Merge(MaxConcurrent)
            .ObserveOn(RxApp.MainThreadScheduler);
    }

    private static Bitmap? BitmapFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    public static string FormatBytes(long bytes, bool round = false)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            dblSByte = bytes / 1024.0;
        return string.Format("{0:0.##} {1}", round ? Math.Ceiling(dblSByte) : dblSByte, suffix[i]);
    }

    public static string GetLargeImageUri(ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_large.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }
}
