using ReactiveUI;
using System;
using System.Diagnostics;
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
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start {url}")
        {
            CreateNoWindow = true,
        });
    }
        
    public static void OpenFolder(AbsolutePath path)
    {
        Process.Start(new ProcessStartInfo(KnownFolders.Windows.Combine("explorer.exe").ToString(), path.ToString())
        {
            CreateNoWindow = true,
        });
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
        LoadingLock loadingLock, HttpClient client)
    {
        return obs
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectTask(async url =>
            {
                var ll = loadingLock.WithLoading();
                try
                {
                    var (found, mStream) = await FindCachedImage(url);
                    if (found) return (ll, mStream);

                    await using var stream = await client.GetStreamAsync(url);

                    var pngStream = new MemoryStream();
                    using (SharpImage img = await SharpImage.LoadAsync(stream))
                    {
                        await img.SaveAsPngAsync(pngStream);
                    }

                    await WriteCachedImage(url, pngStream);
                        
                    return (ll, pngStream);
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return (ll, default);
                }
            })
            .Select(x =>
            {
                var (ll, memStream) = x;
                if (memStream == null) return default;
                try
                {
                    return BitmapImageFromStream(memStream);
                }
                catch (Exception ex)
                {
                    exceptionHandler(ex);
                    return default;
                }
                finally
                {
                    ll.Dispose();
                    memStream.Dispose();
                }
            })
            .ObserveOnGuiThread();
    }

    private static async Task WriteCachedImage(string url, MemoryStream ms)
    {
        var folder = KnownFolders.WabbajackAppLocal.Combine("ModListImages");
        if (!folder.DirectoryExists()) folder.CreateDirectory();

        var path = folder.Combine((await Encoding.UTF8.GetBytes(url).Hash()).ToHex());
            
        await using (var fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write)) {
            ms.WriteTo(fs);
        }
    }

    private static async Task<(bool, MemoryStream)> FindCachedImage(string uri)
    {
        var folder = KnownFolders.WabbajackAppLocal.Combine("ModListImages");
        if (!folder.DirectoryExists()) folder.CreateDirectory();

        var path = folder.Combine((await Encoding.UTF8.GetBytes(uri).Hash()).ToHex());
        if(!path.FileExists()) return (false, default);
        
        var ms = new MemoryStream();
        await using (FileStream fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read))
        {
            await fs.CopyToAsync(ms);
        }
        return (true, ms);
    }

    /// <summary>
    /// Format bytes to a greater unit
    /// </summary>
    /// <param name="bytes">number of bytes</param>
    /// <returns></returns>
    public static string FormatBytes(long bytes)
    {
        string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }

        return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
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
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/refs/heads/master/reports/{metadata.RepositoryName}/{fileName}";
    }
}