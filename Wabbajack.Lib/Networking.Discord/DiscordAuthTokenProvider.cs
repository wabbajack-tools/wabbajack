using System;

namespace Wabbajack.Networking.Discord;

public class DiscordWebHookSetting
{
    public string AuthToken { get; set; } = "";
    public Uri WebHook { get; set; } = new("https://www.wabbajack.org");
}