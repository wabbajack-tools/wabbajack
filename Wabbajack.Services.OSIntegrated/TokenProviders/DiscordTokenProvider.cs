using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Discord;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class DiscordTokenProvider : EncryptedJsonTokenProvider<Dictionary<Channel, DiscordWebHookSetting>>
{
    public DiscordTokenProvider(ILogger<DiscordTokenProvider> logger, DTOSerializer dtos) : base(logger, dtos,
        "discord-endpoints")
    {
    }
}