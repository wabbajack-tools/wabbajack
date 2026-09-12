using System.Collections.Generic;
using System.Linq;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Installer.Test;

/// <summary>
///     Characterization tests for <see cref="FileDeletionRules.ShouldDeleteExistingFile" />.
///
///     This rule decides which of a user's existing files an install is allowed to delete, and several
///     reports of lost data trace back to it. The tests below record what it does today so that a change in
///     behaviour has to be deliberate.
/// </summary>
public class FileDeletionRulesTests
{
    private static readonly AbsolutePath Install = @"C:\Games\Modlist".ToAbsolutePath();
    private static readonly AbsolutePath Downloads = Install.Combine("downloads");
    private static readonly AbsolutePath ModlistArchive = Install.Combine("modlist.wabbajack");

    /// <param name="modListFiles">Paths, relative to the install folder, that the modlist installs.</param>
    /// <param name="bsaPathsToNotBuild">Absolute paths of BSAs that are already up to date.</param>
    private static bool ShouldDelete(string file, IEnumerable<string> modListFiles = null,
        IEnumerable<AbsolutePath> bsaPathsToNotBuild = null)
    {
        var modList = (modListFiles ?? Enumerable.Empty<string>())
            .Select(f => (RelativePath) f)
            .ToHashSet();

        return FileDeletionRules.ShouldDeleteExistingFile(
            Install.Combine(file),
            Install,
            Downloads,
            ModlistArchive,
            modList.Contains,
            (bsaPathsToNotBuild ?? Enumerable.Empty<AbsolutePath>()).ToHashSet());
    }

    [Fact]
    public void DeletesAStrayFile()
    {
        Assert.True(ShouldDelete(@"mods\Leftover\stray.esp"));
    }

    [Fact]
    public void KeepsFilesTheModListInstalls()
    {
        Assert.False(ShouldDelete(@"mods\Good\good.esp", modListFiles: new[] {@"mods\Good\good.esp"}));
    }

    [Fact]
    public void KeepsFilesTheModListInstallsRegardlessOfCase()
    {
        Assert.False(ShouldDelete(@"mods\Good\GOOD.esp", modListFiles: new[] {@"mods\good\good.esp"}));
    }

    [Fact]
    public void KeepsTheModlistArchiveItself()
    {
        Assert.False(ShouldDelete("modlist.wabbajack"));
    }

    [Fact]
    public void KeepsEverythingUnderTheDownloadsFolder()
    {
        Assert.False(ShouldDelete(@"downloads\SomeMod.7z"));
        Assert.False(ShouldDelete(@"downloads\SomeMod.7z.meta"));
        Assert.False(ShouldDelete(@"downloads\nested\deep\SomeMod.7z"));
    }

    [Fact]
    public void KeepsUpToDateBsas()
    {
        var bsa = Install.Combine(@"mods\Output\Textures.bsa");
        Assert.False(ShouldDelete(@"mods\Output\Textures.bsa", bsaPathsToNotBuild: new[] {bsa}));
        Assert.True(ShouldDelete(@"mods\Output\Textures.bsa"));
    }

    [Theory]
    [InlineData(@"mods\[NoDelete]Personal\notes.txt")]
    [InlineData(@"mods\[NoDelete] Personal\notes.txt")]
    [InlineData(@"mods\[No Delete]Personal\notes.txt")]
    [InlineData(@"mods\[nodelete]Personal\notes.txt")]
    [InlineData(@"[NoDelete]\anything.txt")]
    public void KeepsNoDeleteFolders(string file)
    {
        Assert.False(ShouldDelete(file));
    }

    /// <summary>
    ///     The marker only has to follow a path separator, so a file named "[NoDelete].txt" is kept in the
    ///     same way a folder named "[NoDelete]" is.
    /// </summary>
    [Fact]
    public void NoDeleteAlsoMatchesAFileName()
    {
        Assert.False(ShouldDelete(@"mods\Personal\[NoDelete].txt"));
    }

    [Fact]
    public void KeepsSaveFiles()
    {
        Assert.False(ShouldDelete(@"profiles\Default\saves\quicksave.ess"));
    }

    /// <summary>
    ///     Cyberpunk 2077 and others store one save as a folder of files rather than a single file.
    /// </summary>
    [Fact]
    public void KeepsSavesStoredAsFolders()
    {
        Assert.False(ShouldDelete(@"profiles\Default\saves\ManualSave-0\sav.dat"));
        Assert.False(ShouldDelete(@"profiles\Default\saves\ManualSave-0\metadata.9.json"));
        Assert.False(ShouldDelete(@"profiles\Default\saves\ManualSave-0\screenshot.png"));
    }

    [Fact]
    public void KeepsSavesInAnyProfile()
    {
        Assert.False(ShouldDelete(@"profiles\Some Other Profile\saves\quicksave.ess"));
    }

    [Fact]
    public void DeletesNonSaveFilesInsideAProfile()
    {
        Assert.True(ShouldDelete(@"profiles\Default\modlist.txt"));
    }

    [Fact]
    public void DeletesASavesFolderOutsideProfiles()
    {
        Assert.True(ShouldDelete(@"saves\quicksave.ess"));
    }

    /// <summary>
    ///     A "saves" folder placed directly under "profiles", with no profile name in between, is still
    ///     treated as saves. Recorded here because it follows from the depth check rather than from intent.
    /// </summary>
    [Fact]
    public void TreatsSavesDirectlyUnderProfilesAsSaves()
    {
        Assert.False(ShouldDelete(@"profiles\saves\quicksave.ess"));
    }
}
