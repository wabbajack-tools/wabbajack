using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Wabbajack.Paths;

namespace Wabbajack.Compiler;

public class Consts
{
    public static RelativePath BSACreationDir = "TEMP_BSA_FILES".ToRelativePath();
    public static RelativePath MO2ModFolderName = "mods".ToRelativePath();
    public static RelativePath MO2Profiles = "profiles".ToRelativePath();
    public static RelativePath MO2Downloads = "downloads".ToRelativePath();
    public static RelativePath MO2Saves = "saves".ToRelativePath();
    public static Version? CurrentMinimumWabbajackVersion = new("2.2.2.0");

    public static RelativePath GameFolderFilesDir = "Game Folder Files".ToRelativePath();
    public static RelativePath ManualGameFilesDir = "Manual Game Files".ToRelativePath();
    public static RelativePath LOOTFolderFilesDir = "LOOT Config Files".ToRelativePath();
    public static RelativePath MetaIni = "meta.ini".ToRelativePath();
    public static RelativePath ModListTxt = "modlist.txt".ToRelativePath();

    public static string WABBAJACK_INCLUDE = "WABBAJACK_INCLUDE";
    public static string WABBAJACK_ALWAYS_ENABLE = "WABBAJACK_ALWAYS_ENABLE";
    public static string WABBAJACK_ALWAYS_DISABLE = "WABBAJACK_ALWAYS_DISABLE";
    public static string WABBAJACK_NOMATCH_INCLUDE = "WABBAJACK_NOMATCH_INCLUDE";
    public static string WABBAJACK_IGNORE = "WABBAJACK_IGNORE";
    public static RelativePath WABBAJACK_NOMATCH_INCLUDE_FILES = "WABBAJACK_NOMATCH_INCLUDE_FILES.txt".ToRelativePath();
    public static string WABBAJACK_IGNORE_FILES = "WABBAJACK_IGNORE_FILES.txt";
    public static string WABBAJACK_INCLUDE_SAVES = "WABBAJACK_INCLUDE_SAVES";

    public static readonly HashSet<Extension> SupportedBSAs = new[] {".bsa", ".ba2"}
        .Select(s => new Extension(s)).ToHashSet();

    public static HashSet<Extension> ConfigFileExtensions =
        new[] {".json", ".ini", ".yml", ".xml", ".yaml", ".compiler_settings", ".mo2_compiler_settings"}
            .Select(s => new Extension(s)).ToHashSet();

    public static HashSet<Extension> ESPFileExtensions =
        new[] {".esp", ".esm", ".esl"}.Select(s => new Extension(s)).ToHashSet();

    public static HashSet<Extension> AssetFileExtensions =
        new[] {".dds", ".tga", ".nif", ".psc", ".pex"}.Select(s => new Extension(s)).ToHashSet();

    public static string GAME_PATH_MAGIC_BACK = "{--||GAME_PATH_MAGIC_BACK||--}";
    public static string GAME_PATH_MAGIC_DOUBLE_BACK = "{--||GAME_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string GAME_PATH_MAGIC_FORWARD = "{--||GAME_PATH_MAGIC_FORWARD||--}";

    public static string MO2_PATH_MAGIC_BACK = "{--||MO2_PATH_MAGIC_BACK||--}";
    public static string MO2_PATH_MAGIC_DOUBLE_BACK = "{--||MO2_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string MO2_PATH_MAGIC_FORWARD = "{--||MO2_PATH_MAGIC_FORWARD||--}";

    public static string DOWNLOAD_PATH_MAGIC_BACK = "{--||DOWNLOAD_PATH_MAGIC_BACK||--}";
    public static string DOWNLOAD_PATH_MAGIC_DOUBLE_BACK = "{--||DOWNLOAD_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string DOWNLOAD_PATH_MAGIC_FORWARD = "{--||DOWNLOAD_PATH_MAGIC_FORWARD||--}";

    public static string LineSeparator => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\r\n" : "\n";
    public static RelativePath MO2IniName => "ModOrganizer.ini".ToRelativePath();
    public static object CompilerSettings => "compiler_settings.json".ToRelativePath();
}