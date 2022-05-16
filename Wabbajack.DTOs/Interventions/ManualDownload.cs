using System;
using System.Collections.Generic;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Interventions;

public class ManualDownload : AUserIntervention<ManualDownload.BrowserDownloadState>
{
    public Archive Archive { get; }
    public AbsolutePath OutputPath { get; }
    
    public ManualDownload(Archive archive, AbsolutePath outputPath)
    {
        Archive = archive;
        OutputPath = outputPath;
    }

    public record BrowserDownloadState(Uri Uri, Cookie[] Cookies, (string Key, string Value)[] Headers)
    {
        
    }
}