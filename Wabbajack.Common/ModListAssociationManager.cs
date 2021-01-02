using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace Wabbajack.Common
{
    public class ModListAssociationManager
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static readonly string ProgIDPath = "Software\\Classes\\Wabbajack";
        private static readonly string ExtPath = $"Software\\Classes\\{Consts.ModListExtension}";
        
        private static readonly Dictionary<string, string> ProgIDList = new()
        {
            {"", "Wabbajack"},
            {"FriendlyTypeName", "Wabbajack"},
            {"shell\\open\\command",  "\"{execPath}\" -i=\"%1\""},
        };

        private static readonly Dictionary<string, string> ExtList = new()
        {
            {"", "Wabbajack"},
            {"PerceivedType", "Compressed"}
        };

        private static string ResolveExecutablePath(string appPath)
        {
            return Path.GetDirectoryName(appPath) + "\\Wabbajack.exe";
        }

        public static bool NeedsUpdating(string appPath)
        {
#if WINDOWS
            var execPath = ResolveExecutablePath(appPath);
            var progIDKey = Registry.CurrentUser.OpenSubKey(ProgIDPath);
            var tempKey = progIDKey?.OpenSubKey("shell\\open\\command");
            if (progIDKey == null || tempKey == null) return true;
            var value = tempKey.GetValue("");
            return value == null || !string.Equals(value.ToString(), $"\"{execPath}\" -i=\"%1\"");
#endif
#if LINUX
            return false;
#endif
        }

        public static bool IsAssociated()
        {
#if WINDOWS
            var progIDKey = Registry.CurrentUser.OpenSubKey(ProgIDPath);
            var extKey = Registry.CurrentUser.OpenSubKey(ExtPath);
            return progIDKey != null && extKey != null;
#endif
#if LINUX
            return true;
#endif
        }

        public static void Associate(string appPath)
        {
#if WINDOWS
            var execPath = ResolveExecutablePath(appPath);
            var progIDKey = Registry.CurrentUser.CreateSubKey(ProgIDPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            foreach (var entry in ProgIDList)
            {
                if (entry.Key.Contains("\\"))
                {
                    var tempKey = progIDKey?.CreateSubKey(entry.Key);
                    tempKey?.SetValue("", entry.Value.Replace("{execPath}", execPath));
                }
                else
                {
                    progIDKey?.SetValue(entry.Key, entry.Value.Replace("{execPath}", execPath));
                }
            }

            var extKey = Registry.CurrentUser.CreateSubKey(ExtPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            foreach (var entry in ExtList)
            {
                extKey?.SetValue(entry.Key, entry.Value);
            }

            progIDKey?.Close();
            extKey?.Close();
            SHChangeNotify(0x000000, 0x0000, IntPtr.Zero, IntPtr.Zero);      
#endif
        }
    }
}
