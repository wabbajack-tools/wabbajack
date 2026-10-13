using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.ViewModels.Installers;

/// <summary>
///     The folders an install is about to use, and the folders it must stay out of. The machine's own folders are
///     passed in rather than looked up, so the rules read the same in a test on any OS.
/// </summary>
/// <param name="ModlistExists">Whether the chosen .wabbajack file is there.</param>
/// <param name="GameFolders">Every installed game's folder.</param>
/// <param name="SpecialFolderOf">The special folder (Documents, the user profile...) a path is in, or null.</param>
public sealed record InstallPathContext(
    bool ModlistExists,
    AbsolutePath Install,
    AbsolutePath Downloads,
    IReadOnlyList<AbsolutePath> GameFolders,
    AbsolutePath WabbajackFolder,
    AbsolutePath WindowsFolder,
    Func<AbsolutePath, Environment.SpecialFolder?> SpecialFolderOf);

/// <summary>
///     Where an install may go. An install writes thousands of files and deletes ones the list does not expect,
///     so these keep it out of Windows, a game folder or any folder holding one, the Wabbajack folder, special
///     folders such as Documents, and the downloads folder; they are the only thing between a mistyped path and
///     a game or a user profile being overwritten.
/// </summary>
public static class InstallPathRules
{
    public static IEnumerable<ValidationResult> Validate(InstallPathContext c)
    {
        if (!c.ModlistExists)
            yield return ValidationResult.Fail("Wabbajack modlist file does not exist");

        var downloadPath = c.Downloads;
        if (downloadPath.Depth <= 1)
            yield return DownloadsPathValidationResult.Fail("Please specify a download location");

        var installPath = c.Install;
        if (installPath.Depth <= 1)
            yield return InstallPathValidationResult.Fail("Please specify an installation location");
        if (installPath.InFolder(c.WindowsFolder))
            yield return InstallPathValidationResult.Fail("Can't install modlist to Windows folder");

        if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && installPath == downloadPath)
            yield return DownloadsPathValidationResult.Fail("Installation and download locations cannot be identical");

        if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 &&
            KnownFolders.IsSubDirectoryOf(installPath.ToString(), downloadPath.ToString()))
            yield return InstallPathValidationResult.Fail("Can't install to folder within downloads folder");

        foreach (var location in c.GameFolders)
        {
            if (installPath.InFolder(location))
                yield return InstallPathValidationResult.Fail("Can't install modlist into a game folder");

            if (location.ThisAndAllParents().Any(path => installPath == path))
                yield return InstallPathValidationResult.Fail(
                    "Can't install to path, installed files may overwrite game files");
        }

        if (installPath.InFolder(c.WabbajackFolder))
            yield return InstallPathValidationResult.Fail("Can't install a modlist into the Wabbajack folder");
        if (downloadPath.InFolder(c.WabbajackFolder))
            yield return DownloadsPathValidationResult.Fail("Can't download a modlist into the Wabbajack folder");
        if (c.WabbajackFolder.ThisAndAllParents().Any(path => installPath == path))
            yield return InstallPathValidationResult.Fail("Can't install into the Wabbajack folder");

        if (c.SpecialFolderOf(installPath) is { } specialFolder)
            yield return InstallPathValidationResult.Fail($"Can't install into special folder ({specialFolder})");

        if (c.SpecialFolderOf(downloadPath) is { } specialDownloadsFolder)
            yield return DownloadsPathValidationResult.Fail($"Can't download into special folder ({specialDownloadsFolder})");
    }

    /// <summary>
    ///     What the Install button shows: success, or every reason it cannot go yet, one per line. A failure that
    ///     gave no reason still blocks, with a reason saying so.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult?> pickers, IEnumerable<ValidationResult> rules)
    {
        var errors = pickers.Where(v => v != null && v.Failed).Cast<ValidationResult>().Concat(rules).ToArray();
        if (errors.Length == 0) return ValidationResult.Success;

        var reasons = errors.Select(e => e.Reason).Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
        return reasons.Length == 0
            ? ValidationResult.Fail("Validation failed (no reason provided).")
            : ValidationResult.Fail(string.Join("\n", reasons));
    }
}
