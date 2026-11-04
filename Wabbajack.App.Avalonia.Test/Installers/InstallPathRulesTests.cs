using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wabbajack.App.Avalonia.ViewModels.Installers;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Installers;

/// <summary>
///     Where the installer refuses to install. An install writes thousands of files and removes ones the list
///     does not expect, so each rule here stands between a mistyped path and a game, a user profile or Windows
///     being written over. The machine's folders are made up under one root so the table reads the same on
///     every OS.
/// </summary>
public class InstallPathRulesTests
{
    private static readonly AbsolutePath Root = Path.Combine(Path.GetTempPath(), "wj-install-rules").ToAbsolutePath();
    private static readonly AbsolutePath Windows = Root.Combine("Windows");
    private static readonly AbsolutePath Wabbajack = Root.Combine("Tools", "Wabbajack");
    private static readonly AbsolutePath Skyrim = Root.Combine("Games", "Skyrim Special Edition");
    private static readonly AbsolutePath Documents = Root.Combine("Users", "me", "Documents");

    private static InstallPathContext Context(string install, string downloads, bool modlistExists = true)
    {
        return new InstallPathContext(modlistExists, Root.Combine(install), Root.Combine(downloads),
            new[] {Skyrim}, Wabbajack, Windows,
            path => path.InFolder(Documents) || path == Documents ? Environment.SpecialFolder.MyDocuments : null);
    }

    private static string[] Reasons(InstallPathContext context) =>
        InstallPathRules.Validate(context).Select(r => r.Reason).ToArray();

    [Fact]
    public void SeparateFoldersOutsideEverything_AreAccepted()
    {
        Assert.Empty(Reasons(Context(@"Modlists\My List", @"Modlists\My List Downloads")));
    }

    [Theory]
    [InlineData(@"Windows\Lists\My List", "Can't install modlist to Windows folder")]
    [InlineData(@"Games\Skyrim Special Edition\My List", "Can't install modlist into a game folder")]
    [InlineData(@"Games", "Can't install to path, installed files may overwrite game files")]
    [InlineData(@"Tools\Wabbajack\My List", "Can't install a modlist into the Wabbajack folder")]
    [InlineData(@"Tools", "Can't install into the Wabbajack folder")]
    [InlineData(@"Users\me\Documents\My List", "Can't install into special folder (MyDocuments)")]
    [InlineData(@"Downloads\My List", "Can't install to folder within downloads folder")]
    public void Install_IsKeptOutOf(string install, string expected)
    {
        Assert.Contains(expected, Reasons(Context(install, "Downloads")));
    }

    [Theory]
    [InlineData(@"Tools\Wabbajack\downloads", "Can't download a modlist into the Wabbajack folder")]
    [InlineData(@"Users\me\Documents\downloads", "Can't download into special folder (MyDocuments)")]
    public void Downloads_AreKeptOutOf(string downloads, string expected)
    {
        Assert.Contains(expected, Reasons(Context(@"Modlists\My List", downloads)));
    }

    [Fact]
    public void SameFolderForBoth_IsRefused()
    {
        Assert.Contains("Installation and download locations cannot be identical",
            Reasons(Context(@"Modlists\My List", @"Modlists\My List")));
    }

    /// <summary>A drive root is no folder for an install; the pickers start empty, which reads the same.</summary>
    [Fact]
    public void NoFolderChosen_AsksForOne()
    {
        var root = Root.ThisAndAllParents().Last();
        var reasons = Reasons(new InstallPathContext(true, root, root, Array.Empty<AbsolutePath>(), Wabbajack,
            Windows, _ => null));

        Assert.Contains("Please specify an installation location", reasons);
        Assert.Contains("Please specify a download location", reasons);
    }

    [Fact]
    public void MissingModlist_IsReported()
    {
        Assert.Contains("Wabbajack modlist file does not exist",
            Reasons(Context(@"Modlists\My List", "Downloads", modlistExists: false)));
    }

    [Fact]
    public void EveryProblem_IsReportedNotJustTheFirst()
    {
        var reasons = Reasons(Context(@"Games\Skyrim Special Edition\Mods", @"Tools\Wabbajack\dl", modlistExists: false));

        Assert.Equal(3, reasons.Length);
    }

    [Fact]
    public void Button_WithNothingWrong_Succeeds()
    {
        Assert.True(InstallPathRules.Combine(new ValidationResult?[] {null, ValidationResult.Success},
            Array.Empty<ValidationResult>()).Succeeded);
    }

    [Fact]
    public void Button_ListsEveryReasonOnItsOwnLine()
    {
        var result = InstallPathRules.Combine(new ValidationResult?[] {ValidationResult.Fail("Picker says no"), null},
            new[] {ValidationResult.Fail("Rule says no")});

        Assert.True(result.Failed);
        Assert.Equal("Picker says no\nRule says no", result.Reason);
    }

    [Fact]
    public void Button_FailureWithNoReason_StillBlocksAndSaysSo()
    {
        var result = InstallPathRules.Combine(new ValidationResult?[] {ValidationResult.Failure},
            Array.Empty<ValidationResult>());

        Assert.True(result.Failed);
        Assert.Equal("Validation failed (no reason provided).", result.Reason);
    }
}
