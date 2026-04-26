using System;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("MediaFire")]
[JsonAlias("MediaFireDownloader")]
[JsonName("MediaFireDownloader+State, Wabbajack.Lib")]
public class MediaFire : ADownloadState
{
    public Uri Url { get; init; }
    public override string TypeName => "MediaFireDownloader+State";
    public override object[] PrimaryKey => new object[] {Url};
}