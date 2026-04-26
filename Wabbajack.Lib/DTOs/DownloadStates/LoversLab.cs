using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("LoversLab")]
[JsonName("LoversLabOAuthDownloader, Wabbajack.Lib")]
public class LoversLab : IPS4OAuth2
{
    public override string TypeName => "LoversLabOAuthDownloader+State";
}