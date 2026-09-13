using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

public sealed class FakeDownloadPolicySource : IDownloadPolicySource
{
    public ServerAllowList AllowListValue { get; set; } = new()
    {
        AllowedPrefixes = new[] {"https://"},
        GoogleIDs = new string[0]
    };

    public List<Archive> MirrorArchives { get; } = new();

    public Task<ServerAllowList> AllowList(CancellationToken token)
    {
        return Task.FromResult(AllowListValue);
    }

    public Task<ILookup<Hash, Archive>> Mirrors(CancellationToken token)
    {
        return Task.FromResult(MirrorArchives.ToLookup(m => m.Hash));
    }
}
