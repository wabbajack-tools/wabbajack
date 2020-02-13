using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class ModListAssociationManager
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static readonly string ProgIDPath = "Software\\Classes\\Wabbajack";
        private static readonly string ExtPath = $"Software\\Classes\\{Consts.ModListExtension}";
        
        private static readonly Dictionary<string, string> ProgIDList = new Dictionary<string, string>
        {
            {"", "Wabbajack"},
            {"FriendlyTypeName", "Wabbajack"},
            {"shell\\open\\command",  "\"{appPath}\" -i=\"%1\""},
        };

        private static readonly Dictionary<string, string> ExtList = new Dictionary<string, string>
        {
            {"", "Wabbajack"},
            {"PerceivedType", "Compressed"}
        };

        public static bool NeedsUpdating(string appPath)
        {
            var progIDKey = Registry.CurrentUser.OpenSubKey(ProgIDPath);
            var tempKey = progIDKey?.OpenSubKey("shell\\open\\command");
            if (progIDKey == null || tempKey == null) return true;
            var value = tempKey.GetValue("");
            return value == null || !value.ToString().Equals($"\"{appPath}\" -i=\"%1\"");
        }

        public static bool IsAssociated()
        {
            var progIDKey = Registry.CurrentUser.OpenSubKey(ProgIDPath);
            var extKey = Registry.CurrentUser.OpenSubKey(ExtPath);
            return progIDKey != null && extKey != null;
        }

        public static void Associate(string appPath)
        {
            var progIDKey = Registry.CurrentUser.CreateSubKey(ProgIDPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            foreach (var entry in ProgIDList)
            {
                if (entry.Key.Contains("\\"))
                {
                    var tempKey = progIDKey?.CreateSubKey(entry.Key);
                    tempKey?.SetValue("", entry.Value.Replace("{appPath}", appPath));
                }
                else
                {
                    progIDKey?.SetValue(entry.Key, entry.Value.Replace("{appPath}", appPath));
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
        }
    }
}
