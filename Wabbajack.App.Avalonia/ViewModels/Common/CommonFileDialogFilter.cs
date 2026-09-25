using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.App.Avalonia.ViewModels.Common;

/// <summary>
/// Stands in for WindowsAPICodePack's CommonFileDialogFilter, which FilePickerVM.Filters held in the WPF app,
/// so view models that build filters carry over unchanged. Extensions are normalised the way that class did it:
/// trimmed, with "*." and then every remaining "." removed, so "*.wabbajack" becomes "wabbajack".
/// </summary>
public class CommonFileDialogFilter
{
    public string DisplayName { get; }

    public IReadOnlyList<string> Extensions { get; }

    public CommonFileDialogFilter(string rawDisplayName, string extensionList)
    {
        DisplayName = rawDisplayName;
        Extensions = extensionList
            .Split(',', ';')
            .Select(e => e.Trim().Replace("*.", null).Replace(".", null))
            .ToArray();
    }

    /// <summary>The patterns the system dialog is given, which is how WPF's dialog built its filter spec.</summary>
    public IEnumerable<string> Patterns => Extensions.Select(e => "*." + e);

    public override string ToString() => $"{DisplayName} ({string.Join(", ", Patterns)})";
}
