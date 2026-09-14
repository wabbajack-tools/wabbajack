using ReactiveUI;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
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
    public static BitmapImage BitmapImageFromResource(string name) => BitmapImageFromStream(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/" + name)).Stream);

    public static BitmapImage BitmapImageFromStream(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = stream;
        img.EndInit();
        img.Freeze();
        stream.Position = 0;
        if (stream.CanSeek) stream.Position = 0;
        return img;
    }

    public static bool TryGetBitmapImageFromFile(AbsolutePath path, out BitmapImage bitmapImage)
    {
        try
        {
            if (!path.FileExists())
            {
                bitmapImage = default;
                return false;
            }
            bitmapImage = new BitmapImage(new Uri(path.ToString(), UriKind.RelativeOrAbsolute));
            return true;
        }
        catch (Exception)
        {
            bitmapImage = default;
            return false;
        }
    }


    /// <summary>
    ///     Opens a URL in the user's default browser.
    ///     <para>
    ///         Not through <c>cmd.exe /c start</c>, which is what this used to do. <c>cmd</c> reads an
    ///         unquoted <c>&amp;</c> as a command separator, so a URL is cut at its first query parameter and
    ///         the rest is run as a second command that fails out of sight behind <c>CreateNoWindow</c>. A
    ///         Nexus file link - <c>.../mods/266?tab=files&amp;file_id=209150</c> - opened that way landed on
    ///         the mod's Files tab with no file selected, which is the mod page as far as the user is
    ///         concerned. Google Drive's <c>?id=...&amp;export=download</c> lost its download parameter the
    ///         same way. Handing the URL to the shell keeps the whole of it, which also makes the manual
    ///         escaping of spaces unnecessary: <see cref="Uri.AbsoluteUri" /> is already escaped.
    ///     </para>
    /// </summary>
    public static void OpenWebsite(Uri url)
    {
        OpenWebsite(url.AbsoluteUri);
    }

    /// <inheritdoc cref="OpenWebsite(Uri)" />
    public static void OpenWebsite(string url)
    {
        var target = WebsiteTarget(url);
        if (target == null) return;

        Process.Start(new ProcessStartInfo(target) {UseShellExecute = true});
    }

    /// <summary>
    ///     Exactly what <see cref="OpenWebsite(string)" /> hands the shell, or null for something that is
    ///     not a website. Separate from the launch so that the one thing worth pinning - that the whole URL
    ///     survives, query string included - can be tested without opening a browser.
    ///     <para>
    ///         ShellExecute runs whatever it is given, and some of what reaches here comes out of a
    ///         modlist, so only the schemes a website can have are passed on.
    ///     </para>
    ///     <para>
    ///         A bare domain - "www.nexusmods.com/skyrim/mods/1" - is not an absolute URI, but it is what a
    ///         modlist author writes in a readme or website field often enough that <c>cmd /c start</c>
    ///         opening it was load-bearing. A scheme is assumed for those, the way an address bar does,
    ///         rather than leaving the button dead.
    ///     </para>
    /// </summary>
    public static string? WebsiteTarget(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Rejected(url, "it is empty");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            // Only for something with no scheme at all. Uri is lenient enough to accept "https://not a url"
            // with "not" as the host, so the result has to look like a domain before it is used.
            if (!Uri.TryCreate($"{Uri.UriSchemeHttps}://{url}", UriKind.Absolute, out parsed) ||
                !parsed.Host.Contains('.'))
                return Rejected(url, "it is not a URL");
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps &&
            parsed.Scheme != Uri.UriSchemeMailto)
            return Rejected(url, $"\"{parsed.Scheme}\" is not a web address");

        return parsed.AbsoluteUri;
    }

    /// <summary>
    ///     A button that does nothing and says nothing is the failure this whole area is being fixed for, so
    ///     anything dropped here leaves a trace in the log.
    /// </summary>
    private static string? Rejected(string url, string why)
    {
        NLog.LogManager.GetCurrentClassLogger().Warn("Not opening \"{0}\": {1}", url, why);
        return null;
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

    public static AbsolutePath OpenFileDialog(string filter, string initialDirectory = null)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = filter;
        ofd.InitialDirectory = initialDirectory;
        if (ofd.ShowDialog() == DialogResult.OK)
            return (AbsolutePath)ofd.FileName;
        return default;
    }

    public static IObservable<BitmapImage> DownloadBitmapImage(
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