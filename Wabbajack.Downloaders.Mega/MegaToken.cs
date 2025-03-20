using System.Text.Json.Serialization;
using static CG.Web.MegaApiClient.MegaApiClient;

namespace Wabbajack.Downloaders;

public class MegaToken
{
    [JsonPropertyName("login")]
    public AuthInfos? Login { get; set; }
}