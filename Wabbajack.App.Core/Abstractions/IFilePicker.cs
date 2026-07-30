using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Paths;

namespace Wabbajack.Abstractions;

// File/folder selection, decoupled from Microsoft.WindowsAPICodePack shell dialogs.
// The Avalonia head implements this over the Avalonia StorageProvider.
public interface IFilePicker
{
    Task<AbsolutePath?> PickFile(string title, IEnumerable<(string Name, string Pattern)>? filters = null);
    Task<AbsolutePath?> PickFolder(string title);
    Task<AbsolutePath?> SaveFile(string title, string? suggestedName = null);
}
