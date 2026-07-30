using ReactiveUI;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Extensions;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Wabbajack.DTOs;
using Exception = System.Exception;
using SharpImage = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace Wabbajack;

public static class UIUtils
{
    public static Bitmap BitmapImageFromResource(string name) => BitmapImageFromStream(AssetLoader.Open(new Uri("avares://Wabbajack/" + name)));

    public static Bitmap BitmapImageFromStream(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;
        var img = new Bitmap(stream);
        if (stream.CanSeek) stream.Position = 0;
        return img;
    }

    public static bool TryGetBitmapImageFromFile(AbsolutePath path, out Bitmap bitmapImage)
    {
        try
        {
            if (!path.FileExists())
            {
                bitmapImage = default;
                return false;
            }
            bitmapImage = new Bitmap(path.ToString());
            return true;
        }
        catch (Exception)
        {
            bitmapImage = default;
            return false;
        }
    }


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

    public static void OpenFolder(AbsolutePath path)
    {
        string folderPath = path.ToString();
        if (!folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            folderPath += Path.DirectorySeparatorChar.ToString();
        }

        Process.Start(new ProcessStartInfo()
        {
            FileName = folderPath,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    public static void OpenFolderAndSelectFile(AbsolutePath pathToFile)
    {
        Process.Start(new ProcessStartInfo() { FileName = "explorer.exe ", Arguments = $"/select, \"{pathToFile}\"" });
    }

    // Native Win32 shell file dialog (works in an Avalonia app on Windows). `filter` keeps the
    // WinForms-style "Description|*.ext|Description2|*.ext2" format the callers already pass.
    public static AbsolutePath OpenFileDialog(string filter, string initialDirectory = null)
    {
        using var ofd = new CommonOpenFileDialog { EnsureFileExists = true };
        if (initialDirectory != null) ofd.InitialDirectory = initialDirectory;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var parts = filter.Split('|');
            for (var i = 0; i + 1 < parts.Length; i += 2)
                ofd.Filters.Add(new CommonFileDialogFilter(parts[i], parts[i + 1]));
        }
        if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            return (AbsolutePath)ofd.FileName;
        return default;
    }

    public static IObservable<Bitmap> DownloadBitmapImage(
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
                    var (cached, cachedImg) = await icm.Get(url);
                    if (cached) return cachedImg;

                    await using var net = await client.GetStreamAsync(url);

                    using var sharpImg = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Bgra32>(net);
                    const int targetPx = 512;
                    if (sharpImg.Width > targetPx || sharpImg.Height > targetPx)
                    {
                        var scale = Math.Min((float)targetPx / sharpImg.Width, (float)targetPx / sharpImg.Height);
                        var nw = (int)(sharpImg.Width * scale);
                        var nh = (int)(sharpImg.Height * scale);
                        sharpImg.Mutate(x => x.Resize(nw, nh));
                    }

                    using var pngStream = new MemoryStream(capacity: 64 * 1024);
                    var fastPng = new SixLabors.ImageSharp.Formats.Png.PngEncoder
                    {
                        CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.NoCompression,
                        FilterMethod = SixLabors.ImageSharp.Formats.Png.PngFilterMethod.None,
                        BitDepth = SixLabors.ImageSharp.Formats.Png.PngBitDepth.Bit8,
                        ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
                    };
                    try
                    {
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // SME banner failed to load, buggy metadata in log, so this crap removes it
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
                        pngStream.Position = 0;
                        await sharpImg.SaveAsPngAsync(pngStream, fastPng);
                    }

                    pngStream.Position = 0;
                    var bytes = pngStream.ToArray();

                    var img = BitmapImageFromStream(new MemoryStream(bytes, writable: false));

                    await icm.AddBytes(url, bytes);

                    return img;
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return default;
                }
            }))
            .Merge(MaxConcurrent) // limit concurrency
            .ObserveOnGuiThread();
    }


    /// <summary>
    /// Format bytes to a greater unit
    /// </summary>
    /// <param name="bytes">number of bytes</param>
    /// <returns></returns>
    public static string FormatBytes(long bytes, bool round = false)
    {
        string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }

        return String.Format("{0:0.##} {1}", round ? Math.Ceiling(dblSByte) : dblSByte, Suffix[i]);
    }

    public static void OpenFile(AbsolutePath file)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{file}\"")
        {
            CreateNoWindow = true,
        });
    }

    public static string GetSmallImageUri(ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_small.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }
    public static string GetLargeImageUri(ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_large.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }

    public static string GetHumanReadableReadmeLink(string uri)
    {
        if (uri.Contains("raw.githubusercontent.com") && uri.EndsWith(".md"))
        {
            var urlParts = uri.Split('/');
            var user = urlParts[3];
            var repository = urlParts[4];
            var branch = urlParts[5];
            var fileName = urlParts[6];
            return $"https://github.com/{user}/{repository}/blob/{branch}/{fileName}#{repository}";
        }
        return uri;
    }
}