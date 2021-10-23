using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("Mega")]
[JsonName("MegaDownloader, Wabbajack.Lib")]
public class Mega : ADownloadState
{
    public Uri Url { get; init; }
    public override string TypeName => "MegaDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}