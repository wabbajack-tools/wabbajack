using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs;

public class Archive
{
    public Hash Hash { get; set; }
    public string Meta { get; set; } = "";
    public string Name { get; set; }
    public long Size { get; set; }
    public IDownloadState State { get; set; }
}