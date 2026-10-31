using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated.Preflight;

/// <summary>
///     Wraps the login probe and reports when the answer about Nexus Premium differs from the last one this
///     user saw, so every place that asks about the account feeds the same conversion metric.
///     <para>
///         What it remembers is one boolean, in a file beside the metrics key, and that is the whole identity:
///         the metric says a metrics key's account changed tier and never which account. Swapping between a
///         free and a premium account therefore reads as a conversion, which is noise these numbers can bear.
///     </para>
///     <para>
///         Only an answered login counts. Logging out leaves the file alone, so free, logged out, then premium
///         is still a conversion. The file is written after the ping goes out, so one that could not be sent is
///         sent the next time. The ping passes <c>rebound: false</c> because <see cref="Client.SendMetric" />
///         otherwise exits the process on a 403 or a 500, and this runs in the middle of a preflight.
///     </para>
/// </summary>
public class NexusTierTracker : INexusLoginProbe
{
    private static readonly TimeSpan DefaultLimit = TimeSpan.FromSeconds(5);

    private readonly INexusLoginProbe _probe;
    private readonly Client _client;
    private readonly ILogger<NexusTierTracker> _logger;
    private readonly AbsolutePath _appLocal;
    private readonly TimeSpan _limit;

    /// <summary>The startup probe and preflight's can land together, and only one of them may report a change.</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public NexusTierTracker(NexusApiLoginProbe probe, Client client, ILogger<NexusTierTracker> logger)
        : this(probe, client, logger, KnownFolders.WabbajackAppLocal, DefaultLimit)
    {
    }

    internal NexusTierTracker(INexusLoginProbe probe, Client client, ILogger<NexusTierTracker> logger,
        AbsolutePath appLocal, TimeSpan limit)
    {
        _probe = probe;
        _client = client;
        _logger = logger;
        _appLocal = appLocal;
        _limit = limit;
    }

    private AbsolutePath StatePath => _appLocal.Combine("encrypted", "nexus-state");

    public async Task<NexusLoginStatus> Probe(CancellationToken token)
    {
        var status = await _probe.Probe(token);
        if (!status.LoggedIn) return status;

        try
        {
            await Observe(status.IsPremium).WaitAsync(_limit);
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Gave up waiting on the Nexus tier metric");
        }

        return status;
    }

    private async Task Observe(bool isPremium)
    {
        await _lock.WaitAsync();
        try
        {
            var wasPremium = await WasPremium();
            if (wasPremium == isPremium) return;

            var change = NexusTier.Change(wasPremium, isPremium);
            if (change != null)
                await _client.SendMetric(MetricNames.NexusTierChange, change, false);

            await isPremium.AsEncryptedJsonFile(StatePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not record the Nexus tier");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>A file that cannot be read is no baseline, the same as one that is not there.</summary>
    private async Task<bool?> WasPremium()
    {
        try
        {
            return await StatePath.FromEncryptedJsonFile<bool?>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the last Nexus tier");
            return null;
        }
    }
}
