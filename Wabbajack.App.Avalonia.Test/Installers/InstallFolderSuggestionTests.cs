using Wabbajack.App.Avalonia.ViewModels.Installers;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Installers;

/// <summary>
///     The folder name an install suggests for a list it has not installed before. Titles of the form
///     "Name - Subtitle" leave a trailing space before the dash, which used to give an empty word and throw, so
///     every such list was offered no folder at all.
/// </summary>
public class InstallFolderSuggestionTests
{
    [Theory]
    [InlineData("Lorerim", "Lorerim")]
    [InlineData("Living Skyrim", "LivingSkyrim")]
    [InlineData("Halgari's Helper", "HalgarisHelper")]
    [InlineData("Wildlander - The hardcore experience", "Wildlander")]
    [InlineData("Foo Bar - Baz", "FooBar")]
    [InlineData("Elder Scrolls Total Overhaul", "ESTO")]
    [InlineData("A  Dragonborn's   Fate", "ADF")]
    [InlineData("Fallout: New Vegas", "FNV")]
    [InlineData("  Spaced Out  ", "SpacedOut")]
    public void FolderName(string title, string expected)
    {
        Assert.Equal(expected, InstallFolderSuggestion.FolderName(title));
    }

    [Theory]
    [InlineData("/home/me", "Dust and Death", "/home/me/Modlists/DAD")]
    [InlineData(@"D:\", "Lorerim", @"D:\Modlists\Lorerim")]
    public void AListGoesInAModlistsFolderUnderTheRoot(string root, string title, string expected)
    {
        Assert.Equal(expected.ToAbsolutePath(), InstallFolderSuggestion.Folder(root.ToAbsolutePath(), title));
    }
}
