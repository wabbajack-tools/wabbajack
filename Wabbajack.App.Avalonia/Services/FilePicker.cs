using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
/// The system file and folder dialogs, which the WPF app reached through WindowsAPICodePack's
/// CommonOpenFileDialog. On Windows Avalonia opens the same IFileOpenDialog. Everything returns
/// <c>default</c> when the user cancels, as FilePickerVM's TargetPath did.
/// </summary>
public class FilePicker
{
    private static IStorageProvider? Storage =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;

    /// <summary>One existing file. <paramref name="filters" /> are (name, pattern) pairs, e.g. ("Modlist", "modlist.txt").</summary>
    public async Task<AbsolutePath> PickFile(string title, params (string Name, string Pattern)[] filters)
    {
        if (Storage is not { } storage) return default;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters.Select(f => new FilePickerFileType(f.Name) { Patterns = [f.Pattern] }).ToList()
        });

        return files.FirstOrDefault()?.TryGetLocalPath() is { } path ? path.ToAbsolutePath() : default;
    }

    /// <summary>One existing folder.</summary>
    public async Task<AbsolutePath> PickFolder(string title)
    {
        if (Storage is not { } storage) return default;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath() is { } path ? path.ToAbsolutePath() : default;
    }
}
