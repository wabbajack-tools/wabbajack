using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("VectorPlexus")]
[JsonName("VectorPlexusOAuthDownloader+State, Wabbajack.Lib")]
public class VectorPlexus : IPS4OAuth2
{
    public override string TypeName => "VectorPlexusOAuthDownloader+State";
}