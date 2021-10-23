using System;
using System.Diagnostics;

namespace Wabbajack.App;

public static class Utils
{
    public static void OpenWebsiteInExternalBrowser(Uri uri)
    {
        Process.Start(uri.ToString());
    }
}