using System;
using System.Collections.Generic;
using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>A file-type filter, e.g. <c>new FileFilter("Wabbajack Modlist", "*.wabbajack")</c>.</summary>
public sealed class FileFilter
{
    public string Description { get; }
    public IReadOnlyList<string> Patterns { get; }

    public FileFilter(string description, params string[] patterns)
    {
        Description = description;
        Patterns = patterns;
    }
}

public sealed class FileSelectorRequest
{
    public string? Title { get; init; }
    public AbsolutePath InitialDirectory { get; init; }
    public bool IsFolderPicker { get; init; }
    public IReadOnlyList<FileFilter> Filters { get; init; } = Array.Empty<FileFilter>();
}

/// <summary>
/// Platform abstraction for the native file/folder picker. The WPF app supplies a WindowsAPICodePack
/// implementation; an Avalonia app would supply its own. Keeps ViewModels free of any UI-toolkit
/// dialog types so they can live in <c>Wabbajack.App.Core</c>.
/// </summary>
public interface IFileSelector
{
    /// <summary>Shows the picker and returns the chosen path, or null if the user cancelled.</summary>
    AbsolutePath? SelectPath(FileSelectorRequest request);
}
