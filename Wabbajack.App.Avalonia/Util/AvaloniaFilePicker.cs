using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Abstractions;
using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>
/// IFilePicker over Avalonia's StorageProvider, replacing the WPF head's
/// Microsoft.WindowsAPICodePack shell dialogs.
/// </summary>
public class AvaloniaFilePicker : IFilePicker
{
    public async Task<AbsolutePath?> PickFile(string title, IEnumerable<(string Name, string Pattern)>? filters = null)
    {
        var provider = GetStorageProvider();
        if (provider == null) return null;

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = BuildFilters(filters),
        });

        return ToPath(files.FirstOrDefault());
    }

    public async Task<AbsolutePath?> PickFolder(string title)
    {
        var provider = GetStorageProvider();
        if (provider == null) return null;

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return ToPath(folders.FirstOrDefault());
    }

    public async Task<AbsolutePath?> SaveFile(string title, string? suggestedName = null)
    {
        var provider = GetStorageProvider();
        if (provider == null) return null;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
        });

        return ToPath(file);
    }

    // The picker is owned by the window, so it has to be resolved at call time: these view models are
    // constructed during DI setup, before any window exists.
    private static IStorageProvider? GetStorageProvider()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return window == null ? null : TopLevel.GetTopLevel(window)?.StorageProvider;
    }

    // A picked item can live somewhere without a local path (a cloud provider, a virtual shell
    // folder); TryGetLocalPath returns null there, and Wabbajack can only work with real paths.
    private static AbsolutePath? ToPath(IStorageItem? item)
    {
        var local = item?.TryGetLocalPath();
        return string.IsNullOrWhiteSpace(local) ? null : local.ToAbsolutePath();
    }

    private static List<FilePickerFileType>? BuildFilters(IEnumerable<(string Name, string Pattern)>? filters)
    {
        var list = filters?.Select(f => new FilePickerFileType(f.Name) { Patterns = [f.Pattern] }).ToList();
        if (list == null || list.Count == 0) return null;

        list.Add(new FilePickerFileType("All files") { Patterns = ["*"] });
        return list;
    }
}
