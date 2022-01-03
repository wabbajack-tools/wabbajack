using System;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

public static class Consts
{
    public static string AppName = "Wabbajack";
    public static Uri WabbajackBuildServerUri => new("https://build.wabbajack.org");
    public static Version CurrentMinimumWabbajackVersion { get; set; } = Version.Parse("2.3.0.0");
    public static bool UseNetworkWorkaroundMode { get; set; } = false;
    public static AbsolutePath CefCacheLocation { get; } = KnownFolders.WabbajackAppLocal.Combine("Cef");

    public static byte SettingsVersion = 0;

    public static RelativePath NativeSettingsJson = "native_settings.json".ToRelativePath();
}