using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonName("ManualDownloader, Wabbajack.Lib")]
[JsonAlias("Manual")]
public class Manual : ADownloadState
{
    public Uri Url { get; init; }
    
    public string Prompt { get; init; }
    public override string TypeName => "ManualDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}