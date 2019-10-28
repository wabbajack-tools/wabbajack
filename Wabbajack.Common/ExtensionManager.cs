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

        public static bool IsExtensionAssociated()
        {
            return (Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\"+Extension, false) == null);
        }

        public void AssociateExtension()
        {
            var iconPath = "";
            var appPath = "";

            Version winVersion = new Version(6, 2, 9200, 0);

            RegistryKey extReg = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + Extension);
            if(Environment.OSVersion.Platform >= PlatformID.Win32NT && Environment.OSVersion.Version >= winVersion)
                extReg.SetValue("", Extension.Replace(".", "")+"_auto_file");
            RegistryKey appReg = Registry.CurrentUser.CreateSubKey("Software\\Classes\\Applications\\Wabbajack.exe");
            RegistryKey appAssocReg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\"+Extension);

            extReg.CreateSubKey("DefaultIcon").SetValue("", iconPath);
            extReg.CreateSubKey("PerceivedType").SetValue("", "Archive");

            appReg.CreateSubKey("shell\\open\\command").SetValue("", $"\"{appPath}\" -i %i");
            appReg.CreateSubKey("DefaultIcon").SetValue("", iconPath);

            appAssocReg.CreateSubKey("UserChoice").SetValue("Progid", "Applications\\Wabbajack.exe");
            SHChangeNotify(0x000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            if (Environment.OSVersion.Platform >= PlatformID.Win32NT && Environment.OSVersion.Version >= winVersion)
            {
                RegistryKey win8FileReg =
                    Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Extension.Replace(".", "")}_auto_file");
                win8FileReg.CreateSubKey("shell\\open\\command").SetValue("", $"\"{appPath}\" -i %i");
            }
        }
    }
}
