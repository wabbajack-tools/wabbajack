using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.Services;

public class NexusPoll
{
    private readonly NexusApi _api;
    private readonly ParallelOptions _parallelOptions;
    private readonly GlobalInformation _globalInformation;
    private readonly ILogger<NexusPoll> _logger;
    private readonly AppSettings _settings;
    private readonly SqlService _sql;

    public NexusPoll(ILogger<NexusPoll> logger, AppSettings settings, SqlService service,
        GlobalInformation globalInformation, ParallelOptions parallelOptions, NexusApi api)
    {
        _sql = service;
        _settings = settings;
        _globalInformation = globalInformation;
        _logger = logger;
        _parallelOptions = parallelOptions;
        _api = api;
    }

    public async Task UpdateNexusCacheAPI(CancellationToken token)
    {
        using var _ = _logger.BeginScope("Nexus Update via API");
        _logger.Log(LogLevel.Information, "Starting Nexus Update via API");

        var purged = await GameRegistry.Games.Values
            .Where(game => game.NexusName != null)
            .SelectMany(async game =>
            {
                var (mods, _) = await _api.GetUpdates(game.Game, token);

                return mods.Select(mod => new {Game = game, Mod = mod});
            })
            .Select(async row =>
            {
                var a = row.Mod.LatestFileUpdate.AsUnixTime();
                // Mod activity could hide files
                var b = row.Mod.LastestModActivity.AsUnixTime();

                var t = a > b ? a : b;

                long purgeCount = 0;
                purgeCount += await _sql.DeleteNexusModInfosUpdatedBeforeDate(row.Game.Game, row.Mod.ModId, t.Date);
                purgeCount += await _sql.DeleteNexusModFilesUpdatedBeforeDate(row.Game.Game, row.Mod.ModId, t.Date);
                return purgeCount;
            })
            .SumAsync(x => x);

        _logger.Log(LogLevel.Information, "Purged {count} cache entries", purged);
        _globalInformation.LastNexusSyncUTC = DateTime.UtcNow;
    }

    public void Start()
    {
        if (!_settings.RunBackEndJobs) return;

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await UpdateNexusCacheAPI(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting API feed from Nexus");
                }

                await Task.Delay(_globalInformation.NexusAPIPollRate);
            }
        });
    }
}

public static class NexusPollExtensions
{
    public static void UseNexusPoll(this IApplicationBuilder b)
    {
        var poll = (NexusPoll) b.ApplicationServices.GetService(typeof(NexusPoll));
        poll.Start();
    }
}