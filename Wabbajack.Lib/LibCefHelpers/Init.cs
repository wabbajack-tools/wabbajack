using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.LibCefHelpers
{
    public static class Helpers
    {

        /// <summary>
        /// We bundle the cef libs inside the .exe, we need to extract them before loading any wpf code that requires them
        /// </summary>
        public static void ExtractLibs()
        {
            if (File.Exists("cefglue.7z") && File.Exists("libcef.dll")) return;

            using (var fs = File.OpenWrite("cefglue.7z"))
            using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.Lib.LibCefHelpers.cefglue.7z"))
            {
                rs.CopyTo(fs);
                Utils.Log("Extracting libCef files");
            }
            using (var wq = new WorkQueue(1))
                FileExtractor.ExtractAll(wq, "cefglue.7z", ".");

        }
    }
}
