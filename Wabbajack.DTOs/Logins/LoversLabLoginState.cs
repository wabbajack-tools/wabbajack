using System;

namespace Wabbajack.DTOs.Logins;

public class LoversLabLoginState : OAuth2LoginState
{
    public override string SiteName => "Lovers Lab";
    public override string[] Scopes => new[] {"downloads"};
    public override string ClientID => "0b543a010bf1a8f0f4c5dae154fce7c3";
    public override Uri AuthorizationEndpoint => new("https://api.loverslab.com/oauth/authorize/");
    public override Uri TokenEndpoint => new("https://api.loverslab.com/oauth/token/");
}