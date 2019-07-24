using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public static class Consts
    {
        public static string GameFolderFilesDir = "Game Folder Files";
        public static string ModPackMagic = "Celebration!, Cheese for Everyone!";

        public static HashSet<string> SupportedArchives = new HashSet<string>() { ".zip", ".rar", ".7z", ".7zip" };

        public static String UserAgent {
            get
            {
                var platformType = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                var headerString = $"{AppName}/{Assembly.GetEntryAssembly().GetName().Version} ({Environment.OSVersion.VersionString}; {platformType}) {RuntimeInformation.FrameworkDescription}";
                return headerString;
            }
        }

        public static String AppName = "Automaton";
    }
}
