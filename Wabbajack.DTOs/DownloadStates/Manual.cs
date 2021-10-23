using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("ManualDownloader, Wabbajack.Lib")]
[JsonName("Manual")]
public class Manual : ADownloadState
{
    public Uri Url { get; init; }
    public override string TypeName => "ManualDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}