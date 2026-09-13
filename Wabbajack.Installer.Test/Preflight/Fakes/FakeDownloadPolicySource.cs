#nullable enable
using System;
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

    /// <summary>When set, both loads throw it, as they would with no network.</summary>
    public Exception? Throw { get; set; }

    /// <summary>How many times each list was asked for: the plan behind both download checks loads once.</summary>
    public int AllowListCalls { get; private set; }

    public int MirrorCalls { get; private set; }

    public Task<ServerAllowList> AllowList(CancellationToken token)
    {
        AllowListCalls++;
        if (Throw != null) throw Throw;
        return Task.FromResult(AllowListValue);
    }

    public Task<ILookup<Hash, Archive>> Mirrors(CancellationToken token)
    {
        MirrorCalls++;
        if (Throw != null) throw Throw;
        return Task.FromResult(MirrorArchives.ToLookup(m => m.Hash));
    }
}
