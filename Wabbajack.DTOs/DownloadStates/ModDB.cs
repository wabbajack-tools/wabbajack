using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonName("ModDBDownloader, Wabbajack.Lib")]
[JsonAlias("ModDB")]
[JsonAlias("ModDBDownloader")]
public class ModDB : ADownloadState
{
    public Uri Url { get; init; }
    public override string TypeName => "ModDBDownloader";
    public override object[] PrimaryKey => new object[] {Url};
}