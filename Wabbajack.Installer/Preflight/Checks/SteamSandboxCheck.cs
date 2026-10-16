using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Linux only: whether MO2, run under Proton by the Steam the game came from, will be able to reach the
///     install and downloads folders. A Flatpak or Snap Steam runs Proton in its sandbox, and a folder outside it
///     simply is not there for MO2. Nothing stops the install itself, which runs outside the sandbox, so this is
///     a Warning to acknowledge, carrying what grants the access.
/// </summary>
public sealed class SteamSandboxCheck : IPreflightCheck
{
    private readonly Func<string?> _flatpakPermissions;

    public SteamSandboxCheck() : this(ReadFlatpakPermissions)
    {
    }

    /// <param name="flatpakPermissions">What <c>flatpak info --show-permissions</c> prints; null when it cannot run.</param>
    internal SteamSandboxCheck(Func<string?> flatpakPermissions)
    {
        _flatpakPermissions = flatpakPermissions;
    }

    public string Id => PreflightCheckIds.SteamSandbox;
    public string Title => "Steam sandbox";
    public int Order => 150;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.GameInstalled};

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var gameFolder = ctx.State.GameFolder;
        if (gameFolder == default)
            return Task.FromResult(PreflightResult.Passed("No game folder to tell which Steam will run the list"));

        var kind = SteamSandbox.KindOf(gameFolder.ToString(), home,
            nativeInstalled: Directory.Exists(Path.Combine(home, ".local", "share", "Steam", "steamapps")),
            flatpakInstalled: Directory.Exists(SteamSandbox.FlatpakData(home)),
            snapInstalled: Directory.Exists(SteamSandbox.SnapData(home)));

        return Task.FromResult(kind switch
        {
            SteamSandboxKind.Flatpak => CheckFlatpak(ctx, home),
            SteamSandboxKind.Snap => CheckSnap(ctx, home),
            _ => PreflightResult.Passed("Steam is not sandboxed, so Proton can reach every folder")
        });
    }

    private PreflightResult CheckFlatpak(PreflightContext ctx, string home)
    {
        var permissions = _flatpakPermissions();
        if (permissions == null)
            return PreflightResult.Passed("Steam is a Flatpak, but its permissions could not be read",
                $"flatpak info --show-permissions {SteamSandbox.FlatpakAppId} did not run.");

        var granted = SteamSandbox.FlatpakFilesystems(permissions);
        var unreachable = Folders(ctx)
            .Where(f => !SteamSandbox.FlatpakCovers(granted, f.Path, home, name => XdgFolder(name, home)))
            .ToList();
        if (unreachable.Count == 0)
            return PreflightResult.Passed("Flatpak Steam has access to the install and downloads folders");

        var commands = unreachable.Select(f =>
            $"flatpak override --user --filesystem=\"{f.Path}\" {SteamSandbox.FlatpakAppId}");
        return PreflightResult.Warning(
            $"Flatpak Steam cannot reach the {Describe(unreachable)}, so MO2 will not find its files under Proton",
            "Steam is installed from Flathub, and Proton runs inside its sandbox, which has not been given " +
            $"{string.Join(" or ", unreachable.Select(f => f.Path))}. Grant it, then restart Steam:\n" +
            string.Join("\n", commands) + "\nFlatseal can do the same.",
            new[] {PreflightAction.ContinueAnyway});
    }

    private static PreflightResult CheckSnap(PreflightContext ctx, string home)
    {
        var unreachable = Folders(ctx).Where(f => !SteamSandbox.SnapCovers(f.Path, home)).ToList();
        if (unreachable.Count == 0)
            return PreflightResult.Passed("Snap Steam has access to the install and downloads folders");

        return PreflightResult.Warning(
            $"Snap Steam cannot reach the {Describe(unreachable)}, so MO2 will not find its files under Proton",
            "Steam is installed as a snap, which sees only the visible folders in your home folder. Install to a " +
            "folder under home whose name does not start with a dot; a folder under /media, /run/media or /mnt " +
            "also works once the snap is allowed removable media: snap connect steam:removable-media",
            new[] {PreflightAction.ContinueAnyway});
    }

    private static IEnumerable<(string Name, string Path)> Folders(PreflightContext ctx)
    {
        yield return ("install folder", ctx.Config.Install.ToString());
        if (ctx.Config.Downloads != default && !ctx.Config.Downloads.InFolder(ctx.Config.Install))
            yield return ("downloads folder", ctx.Config.Downloads.ToString());
    }

    private static string Describe(IReadOnlyList<(string Name, string Path)> folders)
    {
        return folders.Count == 1 ? folders[0].Name : "install or downloads folder";
    }

    /// <summary>The folder an xdg-download style permission names, as Flatpak resolves it.</summary>
    private static string XdgFolder(string name, string home)
    {
        if (XdgUserDirs.Get(name) is { } named) return named;
        var fallback = name switch
        {
            "DOWNLOAD" => "Downloads",
            "PUBLICSHARE" => "Public",
            _ => name[..1] + name[1..].ToLowerInvariant()
        };
        return $"{home}/{fallback}";
    }

    private static string? ReadFlatpakPermissions()
    {
        var info = new ProcessStartInfo("flatpak")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] {"info", "--show-permissions", SteamSandbox.FlatpakAppId}) info.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(info);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
