using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Paths;

namespace Wabbajack;

public sealed class AvaloniaFileSelector : IFileSelector
{
    public AbsolutePath? SelectPath(FileSelectorRequest request)
    {
        var top = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var provider = top?.StorageProvider;
        if (provider is null) return null;

        IStorageFolder? start = null;
        if (request.InitialDirectory != default)
            start = provider.TryGetFolderFromPathAsync(request.InitialDirectory.ToString()).GetAwaiter().GetResult();

        if (request.IsFolderPicker)
        {
            var folders = provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = request.Title, AllowMultiple = false, SuggestedStartLocation = start
            }).GetAwaiter().GetResult();
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            return path is null ? null : (AbsolutePath) path;
        }

        var filters = request.Filters.Select(f => new FilePickerFileType(f.Description)
        {
            Patterns = f.Patterns.ToList()
        }).ToList();
        var files = provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = request.Title, AllowMultiple = false, SuggestedStartLocation = start,
            FileTypeFilter = filters.Count > 0 ? filters : null
        }).GetAwaiter().GetResult();
        var file = files.FirstOrDefault()?.TryGetLocalPath();
        return file is null ? null : (AbsolutePath) file;
    }
}
