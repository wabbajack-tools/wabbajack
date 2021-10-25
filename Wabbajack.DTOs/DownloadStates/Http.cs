using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("Http")]
[JsonAlias("HttpDownloader")]
[JsonName("HttpDownloader, Wabbajack.Lib")]
public class Http : ADownloadState
{
    public Uri Url { get; init; }
    public string[] Headers { get; set; } = Array.Empty<string>();
    public override string TypeName => "HTTPDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}