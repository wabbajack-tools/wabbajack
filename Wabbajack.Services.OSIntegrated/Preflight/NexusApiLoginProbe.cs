using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Services.OSIntegrated.Preflight;

/// <summary>
///     Asks the Nexus API whether the stored login still works. A token the API rejects reads as expired; a
///     network failure propagates so the check fails with a retry rather than sending the user to log in.
/// </summary>
public class NexusApiLoginProbe : INexusLoginProbe
{
    private readonly NexusApi _api;

    public NexusApiLoginProbe(NexusApi api)
    {
        _api = api;
    }

    public async Task<NexusLoginStatus> Probe(CancellationToken token)
    {
        // NexusApi itself falls back to NEXUS_API_KEY when nothing is stored, so treat that as a token too.
        var hasToken = _api.AuthInfo.HaveToken() ||
                       !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEXUS_API_KEY"));
        if (!hasToken)
            return new NexusLoginStatus(false, false, false, null, null);

        try
        {
            var (info, _) = await _api.Validate(token);
            return new NexusLoginStatus(true, true, info.IsPremium, info.Name, null);
        }
        catch (HttpException ex)
        {
            return new NexusLoginStatus(true, false, false, null, ex.Message);
        }
    }
}
