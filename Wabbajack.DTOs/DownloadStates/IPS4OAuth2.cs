using System;

namespace Wabbajack.DTOs.DownloadStates;

public abstract class IPS4OAuth2 : ADownloadState, IMetaState
{
    public long IPS4Mod { get; set; }

    public bool IsAttachment { get; set; } = false;
    public string IPS4File { get; set; } = "";
    public string IPS4Url { get; set; } = "";

    public override object[] PrimaryKey => new object[] {IPS4Mod, IPS4File ?? "", IsAttachment};
    public Uri URL { get; }
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public Uri? ImageURL { get; set; }
    public bool IsNSFW { get; set; }
    public string? Description { get; set; }
    public Uri? LinkUrl => null;
}