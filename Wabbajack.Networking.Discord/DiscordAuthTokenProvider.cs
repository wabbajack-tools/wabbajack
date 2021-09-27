using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Discord
{
    public class DiscordWebHookSetting
    {
        public string AuthToken { get; set; } = "";
        public Uri WebHook { get; set; } = new("https://www.wabbajack.org");
    }
}