using System;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Utilities;

public static class OSUtil
{
    public static void OpenWebsite(Uri uri)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var helper = new ProcessHelper()
            {
                Path = "cmd.exe".ToRelativePath().RelativeTo(KnownFolders.WindowsSystem32),
                Arguments = new[] {"/C", $"rundll32 url.dll,FileProtocolHandler {uri}"}
            };
            helper.Start().FireAndForget();
        }
        
    }

    public static void OpenFolder(AbsolutePath path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var helper = new ProcessHelper()
            {
                Path = "explorer.exe".ToRelativePath().RelativeTo(KnownFolders.Windows),
                Arguments = new object[] {path}
            };
            helper.Start().FireAndForget();
        }
    }
}