using DynamicData;
using DynamicData.Binding;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Extensions;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack
{
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
            LoadingLock loadingLock)
        {
            return obs
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async url =>
                {
                    var ll = loadingLock.WithLoading();
                    try
                    {
                        var (found, mstream) = await FindCachedImage(url);
                        if (found) return (ll, mstream);
                        
                        var ret = new MemoryStream();
                        using (var client = new HttpClient())
                        await using (var stream = await client.GetStreamAsync(url))
                        {
                            await stream.CopyToAsync(ret);
                        }

                        ret.Seek(0, SeekOrigin.Begin);

                        await WriteCachedImage(url, ret.ToArray());
                        return (ll, ret);
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

        private static async Task WriteCachedImage(string url, byte[] data)
        {
            var folder = KnownFolders.WabbajackAppLocal.Combine("ModListImages");
            if (!folder.DirectoryExists()) folder.CreateDirectory();
            
            var path = folder.Combine((await Encoding.UTF8.GetBytes(url).Hash()).ToHex());
            await path.WriteAllBytesAsync(data);
        }

        private static async Task<(bool Found, MemoryStream data)> FindCachedImage(string uri)
        {
            var folder = KnownFolders.WabbajackAppLocal.Combine("ModListImages");
            if (!folder.DirectoryExists()) folder.CreateDirectory();
            
            var path = folder.Combine((await Encoding.UTF8.GetBytes(uri).Hash()).ToHex());
            return path.FileExists() ? (true, new MemoryStream(await path.ReadAllBytesAsync())) : (false, default);
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
    }
}
