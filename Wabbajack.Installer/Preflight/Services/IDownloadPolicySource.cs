using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Seam over the Wabbajack server's download allow-list, mirror list and proxy index, so the download
///     checks can be tested offline. The production implementation honours <c>Client.IgnoreMirrorList</c>.
/// </summary>
public interface IDownloadPolicySource
{
    Task<ServerAllowList> AllowList(CancellationToken token);

    Task<ILookup<Hash, Archive>> Mirrors(CancellationToken token);

    /// <summary>The files the proxy holds, with <see cref="ProxyIndex.BaseUrl" /> set to where they are served.</summary>
    Task<ProxyIndex> ProxyIndex(CancellationToken token);
}
