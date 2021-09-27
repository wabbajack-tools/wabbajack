using System;
using Wabbajack.Common;

namespace Wabbajack.App
{
    public static class Utils
    {
        public static void OpenWebsiteInExternalBrowser(Uri uri)
        {
            System.Diagnostics.Process.Start(uri.ToString());
        }
    }
}