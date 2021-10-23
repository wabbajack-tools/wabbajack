using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.Services;

public class MirrorQueueService : AbstractService<MirrorQueueService, int>
{
    private DiscordWebHook _discord;
    private readonly SqlService _sql;

    public MirrorQueueService(ILogger<MirrorQueueService> logger, AppSettings settings, QuickSync quickSync,
        DiscordWebHook discordWebHook, SqlService sqlService) :
        base(logger, settings, quickSync, TimeSpan.FromMinutes(5))
    {
        _discord = discordWebHook;
        _sql = sqlService;
    }

    public override async Task<int> Execute()
    {
        await _sql.QueueMirroredFiles();
        return 1;
    }
}