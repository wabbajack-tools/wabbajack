using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Wabbajack.Common
{
    public static class Consts
    {
        public static string GameFolderFilesDir = "Game Folder Files";
        public static string LOOTFolderFilesDir = "LOOT Config Files";
        public static string ModListMagic = "Celebration!, Cheese for Everyone!";
        public static string BSACreationDir = "TEMP_BSA_FILES";
        public static string MegaPrefix = "https://mega.nz/#!";

        public static HashSet<string> SupportedArchives = new HashSet<string> {".zip", ".rar", ".7z", ".7zip", ".fomod", ".omod"};

        public static HashSet<string> SupportedBSAs = new HashSet<string> {".bsa"};

        public static HashSet<string> ConfigFileExtensions = new HashSet<string> {".json", ".ini", ".yml"};
        public static HashSet<string> ESPFileExtensions = new HashSet<string>() { ".esp", ".esm", ".esl"};
        public static HashSet<string> AssetFileExtensions = new HashSet<string>() {".dds", ".tga", ".nif", ".psc", ".pex"};

        public static string NexusCacheDirectory = "nexus_link_cache";

        public static string WABBAJACK_INCLUDE = "WABBAJACK_INCLUDE";
        public static string WABBAJACK_ALWAYS_ENABLE = "WABBAJACK_ALWAYS_ENABLE";
        public static string WABBAJACK_NOMATCH_INCLUDE = "WABBAJACK_NOMATCH_INCLUDE";

        public static string GAME_PATH_MAGIC_BACK = "{--||GAME_PATH_MAGIC_BACK||--}";
        public static string GAME_PATH_MAGIC_DOUBLE_BACK = "{--||GAME_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string GAME_PATH_MAGIC_FORWARD = "{--||GAME_PATH_MAGIC_FORWARD||--}";

        public static string MO2_PATH_MAGIC_BACK = "{--||MO2_PATH_MAGIC_BACK||--}";
        public static string MO2_PATH_MAGIC_DOUBLE_BACK = "{--||MO2_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string MO2_PATH_MAGIC_FORWARD = "{--||MO2_PATH_MAGIC_FORWARD||--}";

        public static string DOWNLOAD_PATH_MAGIC_BACK = "{--||DOWNLOAD_PATH_MAGIC_BACK||--}";
        public static string DOWNLOAD_PATH_MAGIC_DOUBLE_BACK = "{--||DOWNLOAD_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string DOWNLOAD_PATH_MAGIC_FORWARD = "{--||DOWNLOAD_PATH_MAGIC_FORWARD||--}";


        public static string AppName = "Wabbajack";
        public static string HashCacheName = "Wabbajack.hash_cache";

        public static HashSet<string> GameESMs = new HashSet<string>
            {"Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm"};


        public static string UserAgent
        {
            get
            {
                var platformType = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                var headerString =
                    $"{AppName}/{Assembly.GetEntryAssembly().GetName().Version} ({Environment.OSVersion.VersionString}; {platformType}) {RuntimeInformation.FrameworkDescription}";
                return headerString;
            }
        }
    }
}