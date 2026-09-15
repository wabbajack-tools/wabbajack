using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Wabbajack.Paths;

namespace Wabbajack.Installer;

/// <summary>
///     Rules deciding which of a user's existing files an install is allowed to delete.
///
///     These are kept pure and free of I/O so they can be exercised directly; see FileDeletionRulesTests.
///     They run against every file in the install folder and the answer is acted on destructively, so changes
///     here should come with a test.
/// </summary>
public static class FileDeletionRules
{
    private static readonly Regex NoDeleteRegex = new(@"(?i)[\\\/]#{0,5}\[NoDelete\]", RegexOptions.Compiled);

    private static readonly RelativePath SavePath = (RelativePath) "saves";

    /// <summary>
    ///     True when <paramref name="file" /> should be deleted because the modlist does not account for it.
    /// </summary>
    /// <param name="file">An absolute path to a file inside <paramref name="install" />.</param>
    /// <param name="install">Root of the installation.</param>
    /// <param name="downloads">Download folder, preserved in full. May sit outside <paramref name="install" />.</param>
    /// <param name="modlistArchive">The .wabbajack file driving this install.</param>
    /// <param name="isInModList">Answers whether a path relative to <paramref name="install" /> is a modlist file.</param>
    /// <param name="bsaPathsToNotBuild">BSAs already present and up to date, so they are not rebuilt.</param>
    public static bool ShouldDeleteExistingFile(
        AbsolutePath file,
        AbsolutePath install,
        AbsolutePath downloads,
        AbsolutePath modlistArchive,
        Func<RelativePath, bool> isInModList,
        IReadOnlySet<AbsolutePath> bsaPathsToNotBuild)
    {
        if (isInModList(file.RelativeTo(install)) || file.InFolder(downloads))
            return false;

        if (file == modlistArchive)
            return false;

        if (IsSaveFile(file, install))
            return false;

        // Whitespace is stripped so that "[No Delete]" and similar spellings are still honoured.
        var fNoSpaces = new string(file.ToString().Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (NoDeleteRegex.IsMatch(fNoSpaces))
            return false;

        if (bsaPathsToNotBuild.Contains(file))
            return false;

        return true;
    }

    /// <summary>
    ///     True for anything under a "saves" folder belonging to a profile, so that a user's saves survive a
    ///     modlist update. Games that store a save as a folder of files rather than a single file are covered,
    ///     because every level below "saves" is checked rather than only the immediate parent.
    /// </summary>
    private static bool IsSaveFile(AbsolutePath file, AbsolutePath install)
    {
        var profileFolder = install.Combine("profiles");
        if (!file.InFolder(profileFolder))
            return false;

        // The depth filter keeps "profiles" and the profile name itself out of the walk, so a folder named
        // "saves" only counts when it sits inside a profile.
        return file.ThisAndAllParents()
            .Where(path => path.Depth > profileFolder.Depth + 1)
            .Any(path => path.Parent.FileName == SavePath);
    }
}
