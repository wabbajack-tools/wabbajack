using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("WabbajackCDN")]
[JsonName("WabbajackCDNDownloader+State, Wabbajack.Lib")]
public class WabbajackCDN : ADownloadState
{
    public Uri Url { get; init; }
    public override string TypeName => "WabbajackCDNDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}