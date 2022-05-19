using System;
using System.Collections.Generic;
using System.Threading;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Interventions;

public class ManualDownload : AUserIntervention<ManualDownload.BrowserDownloadState>
{
    public Archive Archive { get; }
    
    public ManualDownload(Archive archive)
    {
        Archive = archive;
    }

    public record BrowserDownloadState(Uri Uri, Cookie[] Cookies, (string Key, string Value)[] Headers)
    {
        
    }
}