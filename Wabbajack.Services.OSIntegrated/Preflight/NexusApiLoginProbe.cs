using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Services.OSIntegrated.Preflight;

/// <summary>
///     Asks the Nexus API whether the stored login still works. A token the API rejects reads as expired; a
///     network failure propagates so the check fails with a retry rather than sending the user to log in.
///     <para>
///         What counts as a login is <see cref="NexusCredential.CanDownload" />, the same predicate
///         <c>NexusDownloader.Prepare</c> gates on, so the row can never promise a download the installer
///         would refuse to start. <c>NEXUS_API_KEY</c> is the case that matters: <see cref="NexusApi" /> will
///         happily validate with it, but the downloader cannot use it, so it is reported as what it is -
///         a credential present, no login - rather than as a green row.
///     </para>
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
        var credential = await _api.CredentialSource();
        if (!credential.CanDownload())
            return new NexusLoginStatus(false, false, null, null, credential);

        try
        {
            var (info, _) = await _api.Validate(token);
            return new NexusLoginStatus(true, info.IsPremium, info.Name, null, credential);
        }
        catch (HttpException ex)
        {
            return new NexusLoginStatus(false, false, null, ex.Message, credential);
        }
    }
}
