using System;

namespace Wabbajack.DTOs.DownloadStates;

public interface IMetaState
{
    string? Name { get; set; }
    string? Author { get; set; }
    string? Version { get; set; }
    Uri? ImageURL { get; set; }
    bool IsNSFW { get; set; }
    string? Description { get; set; }
    
    Uri? LinkUrl { get; }
}