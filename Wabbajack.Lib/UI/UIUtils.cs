using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public static class UIUtils
    {

        public static string ShowFolderSelectionDialog(string prompt)
        {
            if (System.Windows.Application.Current.Dispatcher.Thread != Thread.CurrentThread)
            {
                var task = new TaskCompletionSource<string>();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        task.SetResult(ShowFolderSelectionDialog(prompt));
                    }
                    catch (Exception ex)
                    {
                        task.SetException(ex);
                    }
                });
                task.Task.Wait();
                if (task.Task.IsFaulted)
                    throw task.Task.Exception;
                return task.Task.Result;
            }


            var dlg = new CommonOpenFileDialog();
            dlg.Title = prompt;
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = Assembly.GetEntryAssembly().Location;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = Assembly.GetEntryAssembly().Location;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                return dlg.FileName;
                // Do something with selected folder string
            }

            return null;
        }

        public static BitmapImage BitmapImageFromResource(string name) => BitmapImageFromStream(Utils.GetResourceStream(name));

        public static BitmapImage BitmapImageFromStream(Stream stream)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = stream;
            img.EndInit();
            return img;
        }

        public static bool TryGetBitmapImageFromFile(string path, out BitmapImage bitmapImage)
        {
            try
            {
                if (!File.Exists(path))
                {
                    bitmapImage = default;
                    return false;
                }
                bitmapImage = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
                return true;
            }
            catch (Exception)
            {
                bitmapImage = default;
                return false;
            }
        }

        public static string OpenFileDialog(string filter, string initialDirectory = null)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = filter;
            ofd.InitialDirectory = initialDirectory;
            if (ofd.ShowDialog() == DialogResult.OK)
                return ofd.FileName;
            return null;
        }
    }
}
