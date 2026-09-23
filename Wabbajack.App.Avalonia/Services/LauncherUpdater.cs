using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     The WPF app's LauncherUpdater as it actually behaved, run once at startup. When the app is running from a
///     launcher-laid-out install (a version-named folder beside Wabbajack.exe), it deletes all but the two newest
///     older versions. Run from anywhere else, as a development build is, it only logs that it is not updating.
///     <para>
///         The WPF version also meant to replace the launcher with the Wabbajack.exe on the newest GitHub release
///         when that release's tag was newer than the launcher's file version. It never did: it read the release
///         list with System.Text.Json through Newtonsoft attributes, so no tag ever parsed. That step is left out
///         rather than repaired, because released launchers carry file version 0.0.1.0 (release.ps1 does not
///         stamp one on the launcher), so a working comparison would find every release newer and fetch the
///         launcher again on every start. It can come back once the launcher's version is the release's.
///     </para>
/// </summary>
public class LauncherUpdater(ILogger<LauncherUpdater> logger)
{
    public Task Run()
    {
        if (AppFolders.CommonFolder.Value == KnownFolders.EntryPoint)
        {
            logger.LogInformation("Outside of standard install folder, not updating");
            return Task.CompletedTask;
        }

        var version = Version.Parse(KnownFolders.EntryPoint.FileName.ToString());

        var oldVersions = AppFolders.CommonFolder.Value
            .EnumerateDirectories()
            .Select(f => Version.TryParse(f.FileName.ToString(), out var ver) ? (ver, f) : default)
            .Where(f => f != default)
            .Where(f => f.ver < version)
            .OrderByDescending(f => f)
            .Skip(2)
            .ToArray();

        foreach (var (_, path) in oldVersions)
        {
            logger.LogInformation("Deleting old Wabbajack version at: {Path}", path);
            path.DeleteDirectory();
        }

        return Task.CompletedTask;
    }
}
