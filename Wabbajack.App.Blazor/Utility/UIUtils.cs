using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack
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


        public static AbsolutePath OpenFileDialog(string filter, string initialDirectory = null)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = filter;
            ofd.InitialDirectory = initialDirectory;
            if (ofd.ShowDialog() == DialogResult.OK)
                return (AbsolutePath)ofd.FileName;
            return default;
        }
        
    }
}
