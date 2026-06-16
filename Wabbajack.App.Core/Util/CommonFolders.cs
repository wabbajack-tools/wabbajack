using System;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

/// <summary>
/// Toolkit-agnostic helper exposing the "common" install folder (the parent versioned-install
/// folder used to store downloaded mod lists). Extracted from WPF's <c>LauncherUpdater</c> so it
/// can be shared by the WPF and Avalonia apps. Contains only pure-path logic (no WPF/Avalonia
/// types). WPF's <c>LauncherUpdater.CommonFolder</c> delegates here to avoid duplication.
/// </summary>
public static class CommonFolders
{
    public static Lazy<AbsolutePath> CommonFolder = new (() =>
    {
        var entryPoint = KnownFolders.EntryPoint;

        // If we're not in a folder that looks like a version, abort
        if (!Version.TryParse(entryPoint.FileName.ToString(), out var version))
        {
            return entryPoint;
        }

        // If we're not in a folder that has Wabbajack.exe in the parent folder, abort
        if (!entryPoint.Parent.Combine(Consts.AppName).WithExtension(new Extension(".exe")).FileExists())
        {
            return entryPoint;
        }

        return entryPoint.Parent;
    });
}
