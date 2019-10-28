using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class ExtensionManager
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string Extension = ".wabbajack";

        private static readonly string ExtRegPath = $"Software\\Classes\\{Extension}";
        private static readonly string AppRegPath = "Software\\Classes\\Applications\\Wabbajack.exe";
        private static readonly string AppAssocRegPath =
            $"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{Extension}";
        private static readonly string Win8RegPath = $"Software\\Classes\\{Extension.Replace(".", "")}_auto_file";

        public static bool IsExtensionAssociated()
        {
            return (Registry.CurrentUser.OpenSubKey(AppAssocRegPath, false) == null);
        }

        private static bool IsWin8()
        {
            var winVersion = new Version(6, 2, 9200, 0);
            return (Environment.OSVersion.Platform >= PlatformID.Win32NT &&
                    Environment.OSVersion.Version >= winVersion);
        }

        public static bool IsAssociationOutdated(string currentIconPath, string currentAppPath)
        {
            var extReg = Registry.CurrentUser.OpenSubKey(ExtRegPath, false);
            var appReg = Registry.CurrentUser.OpenSubKey(AppAssocRegPath, false);
            var appAssocReg = Registry.CurrentUser.OpenSubKey(AppAssocRegPath, false);

            if (extReg == null || appReg == null || appAssocReg == null) return true;
            if (extReg.OpenSubKey("DefaultIcon", false) == null) return true;
            if (extReg.OpenSubKey("PerceivedType", false) == null) return true;
            if (extReg.OpenSubKey("DefaultIcon", false)?.GetValue("").ToString() != currentIconPath) return true;
            if (appReg.OpenSubKey("shell\\open\\command", false) == null) return true;
            if (appReg.OpenSubKey("DefaultIcon", false) == null) return true;
            if (appReg.OpenSubKey("shell\\open\\command", false)?.GetValue("").ToString() != $"\"{currentAppPath}\" -i %i") return true;
            if (appReg.OpenSubKey("DefaultIcon", false)?.GetValue("").ToString() != currentIconPath) return true;
            if (appAssocReg.OpenSubKey("UserChoice", false) == null) return true;
            if (appAssocReg.OpenSubKey("UserChoice", false)?.GetValue("Progid").ToString() !=
                "Applications\\Wabbajack.exe") return true;

            if (!IsWin8()) return false;

            if (extReg.GetValue("").ToString() != Extension.Replace(".", "") + "_auto_file") return true;
            var win8FileReg = Registry.CurrentUser.OpenSubKey(Win8RegPath, false);
            if (win8FileReg?.OpenSubKey("shell\\open\\command", false) == null) return true;
            return win8FileReg.OpenSubKey("shell\\open\\command", false)?.GetValue("").ToString() !=
                   $"\"{currentAppPath}\" -i %i";
        }

        public static void AssociateExtension(string iconPath, string appPath)
        {
            var extReg = Registry.CurrentUser.CreateSubKey(ExtRegPath);
            if (IsWin8())
                extReg?.SetValue("", Extension.Replace(".", "") + "_auto_file");
            var appReg = Registry.CurrentUser.CreateSubKey(AppRegPath);
            var appAssocReg = Registry.CurrentUser.CreateSubKey(AppAssocRegPath);

            extReg?.CreateSubKey("DefaultIcon")?.SetValue("", iconPath);
            extReg?.CreateSubKey("PerceivedType")?.SetValue("", "Archive");

            appReg?.CreateSubKey("shell\\open\\command")?.SetValue("", $"\"{appPath}\" -i %i");
            appReg?.CreateSubKey("DefaultIcon")?.SetValue("", iconPath);

            appAssocReg?.CreateSubKey("UserChoice")?.SetValue("Progid", "Applications\\Wabbajack.exe");

            if (IsWin8())
            {
                var win8FileReg = Registry.CurrentUser.CreateSubKey(Win8RegPath);
                win8FileReg?.CreateSubKey("shell\\open\\command")?.SetValue("", $"\"{appPath}\" -i %i");
            }
            SHChangeNotify(0x000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
