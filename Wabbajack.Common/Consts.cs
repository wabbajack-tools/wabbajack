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
        public static string LOOTFolderFilesDir = "LOOT Config Files";
        public static string ModPackMagic = "Celebration!, Cheese for Everyone!";
        public static string BSACreationDir = "TEMP_BSA_FILES";
        public static string MegaPrefix = "https://mega.nz/#!";

        public static HashSet<string> SupportedArchives = new HashSet<string> { ".zip", ".rar", ".7z", ".7zip" };
        public static HashSet<string> SupportedBSAs = new HashSet<string> { ".bsa" };

        public static String UserAgent {
            get
            {
                var platformType = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                var headerString = $"{AppName}/{Assembly.GetEntryAssembly().GetName().Version} ({Environment.OSVersion.VersionString}; {platformType}) {RuntimeInformation.FrameworkDescription}";
                return headerString;
            }
        }

        public static HashSet<string> ConfigFileExtensions = new HashSet<string> {".json", ".ini", ".yml"};

        public static string NexusCacheDirectory = "nexus_link_cache";

        public static string WABBAJACK_INCLUDE = "WABBAJACK_INCLUDE";

        public static string GAME_PATH_MAGIC_BACK = "{--||GAME_PATH_MAGIC_BACK||--}";
        public static string GAME_PATH_MAGIC_DOUBLE_BACK = "{--||GAME_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string GAME_PATH_MAGIC_FORWARD = "{--||GAME_PATH_MAGIC_FORWARD||--}";

        public static string MO2_PATH_MAGIC_BACK = "{--||MO2_PATH_MAGIC_BACK||--}";
        public static string MO2_PATH_MAGIC_DOUBLE_BACK = "{--||MO2_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string MO2_PATH_MAGIC_FORWARD = "{--||MO2_PATH_MAGIC_FORWARD||--}";


        public static String AppName = "Wabbajack";
        public static string HashCacheName = "Wabbajack.hash_cache";

        public static HashSet<string> GameESMs = new HashSet<string>() { "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm" };
        public static string WABBAJACK_ALWAYS_ENABLE = "WABBAJACK_ALWAYS_ENABLE";
    }
}
