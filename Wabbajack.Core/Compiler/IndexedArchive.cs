using IniParser.Model;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.VFS;

namespace Wabbajack.Compiler;

/// <summary>
///     Contains all the information we know about a specific archive during compilation
/// </summary>
public class IndexedArchive
{
    public IniData? IniData;
    public string Meta = string.Empty;
    public string Name = string.Empty;

    public IndexedArchive(VirtualFile file)
    {
        File = file;
    }

    public VirtualFile File { get; }
    public IDownloadState? State { get; set; }
}