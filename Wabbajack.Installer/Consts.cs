using Wabbajack.Paths;

namespace Wabbajack.Installer;

public static class Consts
{
    public static string GAME_PATH_MAGIC_BACK = "{--||GAME_PATH_MAGIC_BACK||--}";
    public static string GAME_PATH_MAGIC_DOUBLE_BACK = "{--||GAME_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string GAME_PATH_MAGIC_FORWARD = "{--||GAME_PATH_MAGIC_FORWARD||--}";

    public static string MO2_PATH_MAGIC_BACK = "{--||MO2_PATH_MAGIC_BACK||--}";
    public static string MO2_PATH_MAGIC_DOUBLE_BACK = "{--||MO2_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string MO2_PATH_MAGIC_FORWARD = "{--||MO2_PATH_MAGIC_FORWARD||--}";

    public static string DOWNLOAD_PATH_MAGIC_BACK = "{--||DOWNLOAD_PATH_MAGIC_BACK||--}";
    public static string DOWNLOAD_PATH_MAGIC_DOUBLE_BACK = "{--||DOWNLOAD_PATH_MAGIC_DOUBLE_BACK||--}";
    public static string DOWNLOAD_PATH_MAGIC_FORWARD = "{--||DOWNLOAD_PATH_MAGIC_FORWARD||--}";

    public static RelativePath SettingsIni = "settings.ini".ToRelativePath();

    public static RelativePath MO2ModFolderName = "mods".ToRelativePath();
    public static RelativePath MO2ProfilesFolderName = "profiles".ToRelativePath();

    public const string StepPreparing = "Preparing";
    public const string StepInstalling = "Installing";
    public const string StepDownloading = "Downloading";
    public const string StepHashing = "Hashing";
    public const string StepFinished = "Finished";
    public static RelativePath BSACreationDir = "TEMP_BSA_FILES".ToRelativePath();
}