using System;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("GoogleDrive")]
[JsonName("GoogleDriveDownloader, Wabbajack.Lib")]
public class GoogleDrive : ADownloadState
{
    public string Id { get; init; }

    public override string TypeName => "GoogleDriveDownloader+State";

    [JsonIgnore] public override object[] PrimaryKey => new object[] {Id};
    public Uri GetUri() => new Uri($"https://drive.usercontent.google.com/download?id={Id}");
}