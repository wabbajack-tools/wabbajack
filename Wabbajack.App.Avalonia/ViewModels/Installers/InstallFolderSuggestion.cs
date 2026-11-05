using System;
using System.Linq;
using System.Text.RegularExpressions;
using Humanizer;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.ViewModels.Installers;

/// <summary>The folder name an install is offered for a list it has not been installed from before.</summary>
public static class InstallFolderSuggestion
{
    /// <summary>
    ///     A short folder name from a list's title: what comes before a dash, letters and digits only, PascalCased,
    ///     or the initials of a title of three words or more. Words are split on any run of spaces, so a title like
    ///     "Foo Bar - Baz", whose part before the dash ends in a space, no longer yields an empty word to take an
    ///     initial of (which threw, and left every such list with no suggestion).
    /// </summary>
    /// <summary>Where a list is offered to go: a Modlists folder in <paramref name="root" />, named for it.</summary>
    public static AbsolutePath Folder(AbsolutePath root, string title)
    {
        return root.Combine("Modlists", FolderName(title));
    }

    public static string FolderName(string title)
    {
        var name = title.Split('-')[0];
        name = Regex.Replace(name, "[^a-zA-Z0-9_ .]+", "");
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length >= 3
            ? string.Concat(words.Select(w => w[0])).ToUpper()
            : string.Join(" ", words).Pascalize().Trim();
    }
}
