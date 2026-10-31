using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     What a finished install reports, including which Nexus Mods tier it finished under. The tier comes from a
///     probe of its own because preflight's cannot be relied on: nexus-login only asks when Nexus files are
///     missing, so an install with a full downloads folder would never learn it.
///     <para>
///         The install has already succeeded when this runs, so nothing here is allowed to change that. It is
///         awaited rather than fired and forgotten because the CLI exits straight after, and each step is bounded
///         by <c>limit</c> so a dead connection costs seconds. The tier's ping passes <c>rebound: false</c>, since
///         <see cref="Client.SendMetric" /> otherwise exits the process on a 403 or a 500.
///     </para>
/// </summary>
public sealed class FinishInstallMetrics
{
    private static readonly TimeSpan DefaultLimit = TimeSpan.FromSeconds(5);

    private readonly Client _client;
    private readonly INexusLoginProbe _nexusLogin;
    private readonly ILogger _logger;
    private readonly TimeSpan _limit;

    public FinishInstallMetrics(Client client, INexusLoginProbe nexusLogin, ILogger logger, TimeSpan? limit = null)
    {
        _client = client;
        _nexusLogin = nexusLogin;
        _logger = logger;
        _limit = limit ?? DefaultLimit;
    }

    public async Task Send(string modList)
    {
        await Report(MetricNames.FinishInstall, modList, true);
        await Report($"{MetricNames.FinishInstall}_{NexusTier.Of(await Login())}", modList, false);
    }

    private async Task<NexusLoginStatus?> Login()
    {
        try
        {
            using var limit = new CancellationTokenSource(_limit);
            return await _nexusLogin.Probe(limit.Token).WaitAsync(limit.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check the Nexus Mods account for the install metric");
            return null;
        }
    }

    private async Task Report(string action, string subject, bool rebound)
    {
        try
        {
            await _client.SendMetric(action, subject, rebound).WaitAsync(_limit);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Metric {Action} was not sent", action);
        }
    }
}
