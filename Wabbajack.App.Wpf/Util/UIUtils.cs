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

namespace Wabbajack;

public static class UIUtils
{
    public static BitmapImage BitmapImageFromResource(string name) => BitmapImageFromStream(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/" + name)).Stream);

    public static BitmapImage BitmapImageFromStream(Stream stream)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = stream;
        img.EndInit();
        img.Freeze();
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

    public static AbsolutePath OpenFileDialog(string filter, string initialDirectory = null)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = filter;
        ofd.InitialDirectory = initialDirectory;
        if (ofd.ShowDialog() == DialogResult.OK)
            return (AbsolutePath)ofd.FileName;
        return default;
    }

    public static IObservable<BitmapImage> DownloadBitmapImage(this IObservable<string> obs, Action<Exception> exceptionHandler,
        LoadingLock loadingLock, HttpClient client, ImageCacheManager icm)
    {
        return obs
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectTask(async url =>
            {
                using var ll = loadingLock.WithLoading();
                try
                {
                    var (cached, cachedImg) = await icm.Get(url);
                    if (cached) return cachedImg;

                    await using var stream = await client.GetStreamAsync(url);

                    using var pngStream = new MemoryStream();
                    using (var sharpImg = await SharpImage.LoadAsync(stream))
                    {
                        await sharpImg.SaveAsPngAsync(pngStream);
                    }

                    var img = BitmapImageFromStream(pngStream);
                    await icm.Add(url, img);
                    return img;
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return default;
                }
            })
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