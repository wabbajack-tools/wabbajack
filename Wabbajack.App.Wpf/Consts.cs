using System;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

public static class Consts
{
    public static RelativePath MO2IniName = "ModOrganizer.ini".ToRelativePath();
    public static string AppName = "Wabbajack";
    public static Uri WabbajackBuildServerUri => new("https://build.wabbajack.org");
    public static Uri WabbajackModlistWizardUri => new("https://wizard.wabbajack.org");
    public static Uri WabbajackGithubUri => new("https://github.com/wabbajack-tools/wabbajack");
    public static Uri WabbajackDiscordUri => new("https://discord.gg/wabbajack");
    public static Uri WabbajackPatreonUri => new("https://www.patreon.com/user?u=11907933");
    public static Uri WabbajackWikiUri => new("https://wiki.wabbajack.org");
    public static Version CurrentMinimumWabbajackVersion { get; set; } = Version.Parse("2.3.0.0");
    public static bool UseNetworkWorkaroundMode { get; set; } = false;
    public static AbsolutePath CefCacheLocation { get; } = KnownFolders.WabbajackAppLocal.Combine("Cef");
    public static RelativePath ModListTxt { get; } = "modlist.txt".ToRelativePath();
    public static RelativePath CompilerSettings { get; } = "compiler_settings.json".ToRelativePath();

    public static byte SettingsVersion = 0;

    public static RelativePath NativeSettingsJson = "native_settings.json".ToRelativePath();
    public const string AllSavedCompilerSettingsPaths = "all-compiler-settings-paths";

    // Info - TODO, make rich document?
    public const string FileManagerInfo = @"
Your modlist will contain lots of files and Wabbajack needs to know where all those files came from to compile a modlist installer. Most of these should be mods that are sourced from the downloads folder. But you might have folders you do **not** want to ship with the modlist, or folders or config files that are generated and can be inlined into the .wabbajack installer. Here is where these files or folders are managed.

Find more information on the Wabbajack wiki!

https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html
";
}