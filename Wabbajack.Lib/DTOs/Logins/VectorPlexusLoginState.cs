using System;

namespace Wabbajack.DTOs.Logins;

public class VectorPlexusLoginState : OAuth2LoginState
{
    public override string SiteName => "Vector Plexus";
    public override string[] Scopes => new[] {"profile", "get_downloads"};
    public override string ClientID => "45c6d3c9867903a7daa6ded0a38cedf8";
    public override Uri AuthorizationEndpoint => new("https://vectorplexis.com/oauth/authorize/");
    public override Uri TokenEndpoint => new("https://vectorplexis.com/oauth/token/");
}