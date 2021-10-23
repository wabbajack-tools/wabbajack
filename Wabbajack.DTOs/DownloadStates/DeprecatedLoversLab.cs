using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonName("LoversLabDownloader, Wabbajack.Lib")]
public class DeprecatedLoversLab : ADownloadState
{
    public override string TypeName => "LoversLabDownloader";
    public override object[] PrimaryKey => new[] {""};
}