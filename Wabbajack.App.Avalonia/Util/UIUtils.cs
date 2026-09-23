using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Wabbajack.App.Avalonia.Models;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>The parts of the WPF app's UIUtils that are not tied to WPF, with images as Avalonia bitmaps.</summary>
public static class UIUtils
{
    public static Bitmap BitmapFromStream(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;
        var bitmap = new Bitmap(stream);
        if (stream.CanSeek) stream.Position = 0;
        return bitmap;
    }

    /// <summary>
    ///     Opens a URL in the user's default browser, through the shell rather than <c>cmd /c start</c>, which
    ///     cuts a URL at its first <c>&amp;</c>. See <see cref="WebsiteTarget" /> for what is refused.
    /// </summary>
    public static void OpenWebsite(Uri url) => OpenWebsite(url.AbsoluteUri);

    /// <inheritdoc cref="OpenWebsite(Uri)" />
    public static void OpenWebsite(string url)
    {
        var target = WebsiteTarget(url);
        if (target == null) return;

        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    /// <summary>
    ///     Exactly what <see cref="OpenWebsite(string)" /> hands the shell, or null for something that is not a
    ///     website. ShellExecute runs whatever it is given, and some of what reaches here comes out of a
    ///     modlist, so only the schemes a website can have are passed on. A bare domain gets https, the way an
    ///     address bar does.
    /// </summary>
    public static string? WebsiteTarget(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Rejected(url, "it is empty");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            if (!Uri.TryCreate($"{Uri.UriSchemeHttps}://{url}", UriKind.Absolute, out parsed) ||
                !parsed.Host.Contains('.'))
                return Rejected(url, "it is not a URL");
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps &&
            parsed.Scheme != Uri.UriSchemeMailto)
            return Rejected(url, $"\"{parsed.Scheme}\" is not a web address");

        return parsed.AbsoluteUri;
    }

    private static string? Rejected(string url, string why)
    {
        NLog.LogManager.GetCurrentClassLogger().Warn("Not opening \"{0}\": {1}", url, why);
        return null;
    }

    /// <summary>Bytes in the largest unit that keeps the number at least one, to two places or rounded up.</summary>
    public static string FormatBytes(long bytes, bool round = false)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            dblSByte = bytes / 1024.0;

        return string.Format("{0:0.##} {1}", round ? Math.Ceiling(dblSByte) : dblSByte, suffix[i]);
    }

    /// <summary>The gallery's image for a modlist, from the mod-lists repository's reports.</summary>
    public static string GetLargeImageUri(Wabbajack.DTOs.ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_large.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }

    /// <summary>Opens Explorer on the file's folder with the file selected.</summary>
    public static void OpenFolderAndSelectFile(AbsolutePath pathToFile)
    {
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe ", Arguments = $"/select, \"{pathToFile}\"" });
    }

    /// <summary>Opens a file with whatever the shell has registered for it.</summary>
    public static void OpenFile(AbsolutePath file)
    {
        Process.Start(new ProcessStartInfo(file.ToString()) { UseShellExecute = true });
    }

    public static void OpenFolder(AbsolutePath path)
    {
        var folderPath = path.ToString();
        if (!folderPath.EndsWith(Path.DirectorySeparatorChar))
            folderPath += Path.DirectorySeparatorChar;

        Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true, Verb = "open" });
    }

    /// <summary>
    ///     Fetches images for display: from the cache when it has them, otherwise downloaded, shrunk to at most
    ///     512px, re-encoded as PNG, cached and returned. Eight at a time. A failure goes to
    ///     <paramref name="exceptionHandler" /> and produces null rather than ending the stream.
    /// </summary>
    public static IObservable<Bitmap?> DownloadBitmap(
        this IObservable<string> urls,
        Action<Exception> exceptionHandler,
        LoadingLock loadingLock,
        HttpClient client,
        ImageCacheManager icm)
    {
        const int maxConcurrent = 8;

        return urls
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(url => Observable.FromAsync(async () =>
            {
                using var ll = loadingLock.WithLoading();
                try
                {
                    var (cached, cachedImg) = await icm.Get(url);
                    if (cached) return cachedImg;

                    await using var net = await client.GetStreamAsync(url);
                    using var sharpImg = await SixLabors.ImageSharp.Image.LoadAsync<Bgra32>(net);
                    const int targetPx = 512;
                    if (sharpImg.Width > targetPx || sharpImg.Height > targetPx)
                    {
                        var scale = Math.Min((float)targetPx / sharpImg.Width, (float)targetPx / sharpImg.Height);
                        sharpImg.Mutate(x => x.Resize((int)(sharpImg.Width * scale), (int)(sharpImg.Height * scale)));
                    }

                    using var pngStream = new MemoryStream(64 * 1024);
                    var fastPng = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.NoCompression,
                        FilterMethod = PngFilterMethod.None,
                        BitDepth = PngBitDepth.Bit8,
                        ColorType = PngColorType.RgbWithAlpha
                    };
                    try
                    {
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Some banners carry metadata ImageSharp cannot write back; drop it and try again.
                        sharpImg.Metadata.IccProfile = null;
                        sharpImg.Metadata.ExifProfile = null;
                        sharpImg.Metadata.XmpProfile = null;
                        foreach (var f in sharpImg.Frames)
                        {
                            f.Metadata.IccProfile = null;
                            f.Metadata.ExifProfile = null;
                            f.Metadata.XmpProfile = null;
                        }

                        pngStream.SetLength(0);
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }

                    var bytes = pngStream.ToArray();
                    var img = BitmapFromStream(new MemoryStream(bytes, false));
                    await icm.AddBytes(url, bytes);
                    return img;
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return null;
                }
            }))
            .Merge(maxConcurrent)
            .ObserveOn(RxApp.MainThreadScheduler);
    }
}
