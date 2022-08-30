using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common.IO;

namespace Wabbajack.Common
{
    public static class Consts
    {
        public static Version? CurrentMinimumWabbajackVersion = new("2.2.2.0");
        public static Version MaximumUnsupportedWindowsVersion = new("10.0.18363.0");
        public static bool TestMode { get; set; } = false;

        public static RelativePath GameFolderFilesDir = (RelativePath)"Game Folder Files";
        public static RelativePath ManualGameFilesDir = (RelativePath)"Manual Game Files";
        public static RelativePath LOOTFolderFilesDir = (RelativePath)"LOOT Config Files";
        public static RelativePath BSACreationDir = (RelativePath)"TEMP_BSA_FILES";

        public static AbsolutePath ModListDownloadFolder => "downloaded_mod_lists".RelativeTo(AbsolutePath.EntryPoint);

        public static string MegaPrefix = "https://mega.nz/#!";
        public static string MegaFilePrefix = "https://mega.nz/file/";

        public static readonly HashSet<Extension> SupportedArchives = new[]{".zip", ".rar", ".7z", ".7zip", ".fomod", ".omod", ".exe", ".dat", ".gz", ".tar"}
            .Select(s => new Extension(s)).ToHashSet();

        // HashSet with archive extensions that need to be tested before extraction
        public static HashSet<Extension> TestArchivesBeforeExtraction = new []{".dat"}.Select(s => new Extension(s)).ToHashSet();

        public static readonly HashSet<Extension> SupportedBSAs = new[] {".bsa", ".ba2"}
            .Select(s => new Extension(s)).ToHashSet();

        public static HashSet<Extension> ConfigFileExtensions = new[]{".json", ".ini", ".yml", ".xml", ".compiler_settings"}.Select(s => new Extension(s)).ToHashSet();
        public static HashSet<Extension> ESPFileExtensions = new []{ ".esp", ".esm", ".esl"}.Select(s => new Extension(s)).ToHashSet();
        public static HashSet<Extension> AssetFileExtensions = new[] {".dds", ".tga", ".nif", ".psc", ".pex"}.Select(s => new Extension(s)).ToHashSet();
        
        public static readonly Extension EXE = new Extension(".exe");
        public static readonly Extension OMOD = new Extension(".omod");
        public static readonly Extension ESM = new Extension(".esm");
        public static readonly Extension ESP = new Extension(".esp");

        public static string WABBAJACK_INCLUDE = "WABBAJACK_INCLUDE";
        public static string WABBAJACK_ALWAYS_ENABLE = "WABBAJACK_ALWAYS_ENABLE";
        public static string WABBAJACK_ALWAYS_DISABLE = "WABBAJACK_ALWAYS_DISABLE";
        public static string WABBAJACK_NOMATCH_INCLUDE = "WABBAJACK_NOMATCH_INCLUDE";
        public static string WABBAJACK_IGNORE = "WABBAJACK_IGNORE";
        public static string WABBAJACK_NOMATCH_INCLUDE_FILES = "WABBAJACK_NOMATCH_INCLUDE_FILES.txt";
        public static string WABBAJACK_IGNORE_FILES = "WABBAJACK_IGNORE_FILES.txt";
        public static string WABBAJACK_INCLUDE_SAVES = "WABBAJACK_INCLUDE_SAVES";

        public static string GAME_PATH_MAGIC_BACK = "{--||GAME_PATH_MAGIC_BACK||--}";
        public static string GAME_PATH_MAGIC_DOUBLE_BACK = "{--||GAME_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string GAME_PATH_MAGIC_FORWARD = "{--||GAME_PATH_MAGIC_FORWARD||--}";

        public static string MO2_PATH_MAGIC_BACK = "{--||MO2_PATH_MAGIC_BACK||--}";
        public static string MO2_PATH_MAGIC_DOUBLE_BACK = "{--||MO2_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string MO2_PATH_MAGIC_FORWARD = "{--||MO2_PATH_MAGIC_FORWARD||--}";

        public static string DOWNLOAD_PATH_MAGIC_BACK = "{--||DOWNLOAD_PATH_MAGIC_BACK||--}";
        public static string DOWNLOAD_PATH_MAGIC_DOUBLE_BACK = "{--||DOWNLOAD_PATH_MAGIC_DOUBLE_BACK||--}";
        public static string DOWNLOAD_PATH_MAGIC_FORWARD = "{--||DOWNLOAD_PATH_MAGIC_FORWARD||--}";
        
        public static Uri GITHUB_REPO_RELEASES = new Uri("https://api.github.com/repos/wabbajack-tools/wabbajack/releases");


        public static string AppName = "Wabbajack";

        public static HashSet<RelativePath> GameESMs = new []
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

        }.Select(s => (RelativePath)s).ToHashSet();

        public static string ServerWhitelistURL = "https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml";
        public static string ModlistMetadataURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/modlists.json";
        public static string UtilityModlistMetadataURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/utility_modlists.json";
        public static string UnlistedModlistMetadataURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/unlisted_modlists.json";
        public static string ModlistSummaryURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/modListSummary.json";
        public static string UpgradedFilesURL = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/upgraded.json";
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
        
        public static RelativePath MetaIni = new("meta.ini");
        public static Extension IniExtension = new(".ini");

        public static Extension HashFileExtension = new(".xxHash");
        public static Extension MetaFileExtension = new(".meta");
        public const string ModListExtensionString = ".wabbajack";
        public static Extension ModListExtension = new(ModListExtensionString);
        public static AbsolutePath LocalAppDataPath => new(Path.Combine(KnownFolders.LocalAppData.Path, "Wabbajack"));
        public static AbsolutePath FaviconCacheFolderPath => LocalAppDataPath.Combine("favicons");
        public static string MetricsKeyHeader => "x-metrics-key";

        public static string WabbajackCacheLocation = "http://build.wabbajack.org/nexus_api_cache/";

        public static string WabbajackCacheHostname = "build.wabbajack.org";
        
        // Direct IP to the CDN
        public static string NetworkWorkaroundHost = "51.81.80.6";
        public static bool UseNetworkWorkaroundMode = false;
        
        public static Uri WabbajackBuildServerUri = new Uri("https://build.wabbajack.org");
        public static int WabbajackCachePort = 80;
        public static int MaxHTTPRetries = 4;
        public static RelativePath MO2ModFolderName = (RelativePath)"mods";
        public static RelativePath MO2ProfilesFolderName = (RelativePath)"profiles";

        public static AbsolutePath PatchCacheFolder => LocalAppDataPath.Combine("patch_cache");
        public static int MaxConnectionsPerServer = 4;

        public static AbsolutePath LogsFolder { get; set; } = ((RelativePath)"logs").RelativeToEntryPoint();
        public static AbsolutePath EntryPoint => (AbsolutePath)(Assembly.GetEntryAssembly()?.Location ?? (string)((RelativePath)"Unknown").RelativeToWorkingDirectory());
        public static AbsolutePath LogFile => LogsFolder.Combine(EntryPoint.FileNameWithoutExtension + ".current.log");
        public static int MaxOldLogs = 50;
        public static Extension BSA = new(".BSA");
        public static Extension MOHIDDEN = new(".mohidden");

        public static AbsolutePath SettingsFile => LocalAppDataPath.Combine("settings.json");
        public static RelativePath SettingsIni = (RelativePath)"settings.ini";
        public static byte SettingsVersion => 2;
        public static TimeSpan MaxVerifyTime => TimeSpan.FromMinutes(10);
        public static Extension ModlistMetadataExtension = new(".modlist_metadata");

        public static readonly string WabbajackAuthoredFilesPrefix = "https://wabbajack.b-cdn.net/";

        public static RelativePath NativeSettingsJson = (RelativePath)"native_compiler_settings.json";

        public static bool IsServer = false;

        public static string CompressedBodyHeader = "x-compressed-body";

        public static AbsolutePath CefCacheLocation = @"CEF".RelativeTo(LocalAppDataPath);

        public static Extension SeqExtension = new Extension(".seq");

        public static RelativePath SettingsJson = (RelativePath)"settings.json";

        public static Extension TempExtension = new Extension(".temp");

        public static Extension OctoSig = new Extension(".octo_sig");

        public static RelativePath ModListTxt = (RelativePath)"modlist.txt";
        public static RelativePath ModOrganizer2Exe = (RelativePath)"ModOrganizer.exe";
        public static RelativePath ModOrganizer2Ini = (RelativePath)"ModOrganizer.ini";
        public static readonly string AuthorAPIKeyFile = "author-api-key.txt";

        public static readonly Uri WabbajackOrg = new Uri("https://www.wabbajack.org/");
        
        public static readonly long UploadedFileBlockSize = (long)1024 * 1024 * 2;

        public static readonly HashSet<Extension> TextureExtensions = new() {new Extension(".dds"), new Extension(".tga")};
    }
}
