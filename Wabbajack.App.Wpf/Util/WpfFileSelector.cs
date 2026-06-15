using System.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>WPF/WindowsAPICodePack implementation of <see cref="IFileSelector"/>.</summary>
public sealed class WpfFileSelector : IFileSelector
{
    public AbsolutePath? SelectPath(FileSelectorRequest request)
    {
        var initialDir = request.InitialDirectory == default ? null : request.InitialDirectory.ToString();
        var dlg = new CommonOpenFileDialog
        {
            Title = request.Title,
            IsFolderPicker = request.IsFolderPicker,
            InitialDirectory = initialDir,
            DefaultDirectory = initialDir,
            AddToMostRecentlyUsedList = false,
            AllowNonFileSystemItems = false,
            EnsureFileExists = true,
            EnsurePathExists = true,
            EnsureReadOnly = false,
            EnsureValidNames = true,
            Multiselect = false,
            ShowPlacesList = true,
        };

        foreach (var filter in request.Filters)
            dlg.Filters.Add(new CommonFileDialogFilter(filter.Description, string.Join(";", filter.Patterns)));

        if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return null;

        return (AbsolutePath) dlg.FileName;
    }
}
