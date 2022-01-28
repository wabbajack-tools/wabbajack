using System;
using System.Diagnostics;
using System.Windows.Forms;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Blazor.Utility
{
    public static class UIUtils
    {
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


        public static AbsolutePath OpenFileDialog(string filter, string? initialDirectory = null)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = filter;
            ofd.InitialDirectory = initialDirectory ?? string.Empty;
            if (ofd.ShowDialog() == DialogResult.OK)
                return (AbsolutePath)ofd.FileName;
            return default;
        }
        
    }
}
