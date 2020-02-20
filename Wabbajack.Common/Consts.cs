using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common.IO;

namespace Wabbajack.Common
{
    public static class Consts
    {
        public static bool TestMode { get; set; } = false;

        public static string GameFolderFilesDir = "Game Folder Files";
        public static string ManualGameFilesDir = "Manual Game Files";
        public static string LOOTFolderFilesDir = "LOOT Config Files";
        public static string BSACreationDir = "TEMP_BSA_FILES";

        public static string ModListDownloadFolder = "downloaded_mod_lists";

        public static string MegaPrefix = "https://mega.nz/#!";

        public static HashSet<string> SupportedArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {".zip", ".rar", ".7z", ".7zip", ".fomod", ".omod", ".exe", ".dat"};

        // HashSet with archive extensions that need to be tested before extraction
        public static HashSet<string> TestArchivesBeforeExtraction = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {".dat"};

        public static HashSet<string> SupportedBSAs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {".bsa", ".ba2"};

        public static HashSet<string> ConfigFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {".json", ".ini", ".yml", ".xml"};
        public static HashSet<string> ESPFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".esp", ".esm", ".esl"};
        public static HashSet<string> AssetFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {".dds", ".tga", ".nif", ".psc", ".pex"};

        public static string NexusCacheDirectory = "nexus_link_cache";

        public static string WABBAJACK_INCLUDE = "WABBAJACK_INCLUDE";
        public static string WABBAJACK_ALWAYS_ENABLE = "WABBAJACK_ALWAYS_ENABLE";
        public static string WABBAJACK_NOMATCH_INCLUDE = "WABBAJACK_NOMATCH_INCLUDE";
        public static string WABBAJACK_VORTEX_MANUAL = "WABBAJACK_VORTEX_MANUAL";

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

        public static HashSet<string> GameESMs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Skyrim LE/SE
            "Skyrim.esm", 
            "Update.esm", 
            "Dawnguard.esm", 
            "HearthFires.esm", 
            "Dragonborn.esm",

            // Fallout 4
            "DLCRobot.esm",
            "DLCworkshop01.esm",
            "DLCCoast.esm",
            "DLCworkshop02.esm",
            "DLCworkshop03.esm",
            "DLCNukaWorld.esm",
            "DLCUltraHighResolution.esm"

        };

        public static string ModPermissionsURL = "https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/NexusModPermissions.yml";
        public static string ServerWhitelistURL = "https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml";
        public static string ModlistMetadataURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/modlists.json";
        public static string ModlistSummaryURL = "http://build.wabbajack.org/lists/status.json";
        public static string UserAgent
        {
            get
            {
                var platformType = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                var headerString =
                    $"{AppName}/{Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 1)} ({Environment.OSVersion.VersionString}; {platformType}) {RuntimeInformation.FrameworkDescription}";
                return headerString;
            }
        }

        public static string HashFileExtension => ".xxHash";
        public static string MetaFileExtension => ".meta";
        public static string ModListExtension = ".wabbajack";
        public static string LocalAppDataPath => Path.Combine(KnownFolders.LocalAppData.Path, "Wabbajack");
        public static string MetricsKeyHeader => "x-metrics-key";

        public static string WabbajackCacheLocation = "http://build.wabbajack.org/nexus_api_cache/";

        public static string WabbajackCacheHostname = "build.wabbajack.org";
        public static int WabbajackCachePort = 80;
        public static int MaxHTTPRetries = 4;
        public const string MO2ModFolderName = "mods";

        public static string PatchCacheFolder => Path.Combine(LocalAppDataPath, "patch_cache");
        public static int MaxConnectionsPerServer = 4;

        public static string LogsFolder = "logs";
        public static int MaxOldLogs = 50;
    }
}
