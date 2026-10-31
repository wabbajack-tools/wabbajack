using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

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
    public Task<AbsolutePath> PickFile(string title, params (string Name, string Pattern)[] filters)
        => PickFile(title, default, filters);

    /// <summary>
    ///     One existing file, with the dialog opened in <paramref name="startIn" /> when that folder exists, as
    ///     the WPF OpenFileDialog's InitialDirectory did.
    /// </summary>
    public async Task<AbsolutePath> PickFile(string title, AbsolutePath startIn, params (string Name, string Pattern)[] filters)
    {
        if (Storage is not { } storage) return default;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await StartFolder(storage, startIn),
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

    private static async Task<IStorageFolder?> StartFolder(IStorageProvider storage, AbsolutePath folder)
    {
        if (folder == default || !folder.DirectoryExists()) return null;
        return await storage.TryGetFolderFromPathAsync(folder.ToString());
    }
}
