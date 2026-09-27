using System;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Util;

public static class AppFolders
{
    /// <summary>
    /// The WPF app's LauncherUpdater.CommonFolder: the folder above the versioned one when the app runs from
    /// an install laid out by the launcher (a version-named folder beside Wabbajack.exe), otherwise the app's
    /// own folder. Downloaded modlists live under it so every version shares them.
    /// </summary>
    public static readonly Lazy<AbsolutePath> CommonFolder = new(() =>
    {
        var entryPoint = KnownFolders.EntryPoint;

        if (!Version.TryParse(entryPoint.FileName.ToString(), out _))
            return entryPoint;

        if (!entryPoint.Parent.Combine("Wabbajack").WithExtension(new Extension(".exe")).FileExists())
            return entryPoint;

        return entryPoint.Parent;
    });
}
