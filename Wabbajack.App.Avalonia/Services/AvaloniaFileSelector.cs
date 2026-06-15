using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>
/// Avalonia implementation of <see cref="IFileSelector"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Known limitation (Wave 0):</strong> <see cref="IFileSelector.SelectPath"/> is synchronous, but
/// Avalonia's storage-picker APIs are async. The current implementation bridges this with
/// <c>.GetAwaiter().GetResult()</c>, which will <strong>deadlock</strong> if called on the UI thread
/// (the awaited Task needs the UI thread to complete, but the UI thread is blocked waiting for it).
/// </para>
/// <para>
/// This is safe only when called from a background thread. <c>FilePickerVM</c> is not exercised in
/// Wave 0, so the risk is contained for now. In a later wave, <see cref="IFileSelector"/> should be
/// updated to expose an async signature (<c>Task&lt;AbsolutePath?&gt;</c>) to eliminate the
/// sync-over-async pattern entirely.
/// </para>
/// </remarks>
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
