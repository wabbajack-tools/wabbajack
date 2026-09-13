using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.Services.OSIntegrated.Preflight;

/// <summary>
///     The allow-list and mirror list from the Wabbajack server. <c>Client.LoadMirrors</c> already returns
///     nothing when <c>IgnoreMirrorList</c> is set.
/// </summary>
public class ClientDownloadPolicySource : IDownloadPolicySource
{
    private readonly Client _client;

    public ClientDownloadPolicySource(Client client)
    {
        _client = client;
    }

    public Task<ServerAllowList> AllowList(CancellationToken token)
    {
        return _client.LoadDownloadAllowList();
    }

    public async Task<ILookup<Hash, Archive>> Mirrors(CancellationToken token)
    {
        return (await _client.LoadMirrors()).ToLookup(m => m.Hash);
    }
}
