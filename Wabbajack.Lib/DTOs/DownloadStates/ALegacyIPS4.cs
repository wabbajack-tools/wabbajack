using System;

namespace Wabbajack.DTOs.DownloadStates;

public abstract class ALegacyIPS4 : IDownloadState
{
    public Uri FullURL { get; set; } = new("https://www.wabbajack.org");
    public bool IsAttachment { get; set; }
    public string FileID { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public object[] PrimaryKey { get; }
    public string TypeName { get; }
    public string PrimaryKeyString { get; }
}