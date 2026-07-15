using System;

namespace Wabbajack.DTOs;

public class ModlistViewerListEntry
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Game { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime Updated { get; set; }
}
