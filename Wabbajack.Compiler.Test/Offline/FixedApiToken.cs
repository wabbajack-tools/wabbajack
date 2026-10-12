using System.Threading.Tasks;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Compiler.Test.Offline;

/// <summary>
///     The Wabbajack API token the compiler tests send to <see cref="OfflineServers" />. The real provider
///     creates a metrics key under the user's app data folder on first use, and the tests running in parallel
///     on a fresh machine all tried to create it at once. Nothing reads the key back, so a fixed one in memory
///     does the job without touching that folder.
/// </summary>
public class FixedApiToken : ITokenProvider<WabbajackApiState>
{
    private static readonly WabbajackApiState State = new() {MetricsKey = "offline-compiler-tests"};

    public ValueTask<WabbajackApiState?> Get()
    {
        return ValueTask.FromResult<WabbajackApiState?>(State);
    }

    public ValueTask SetToken(WabbajackApiState val)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> Delete()
    {
        return ValueTask.FromResult(false);
    }

    public bool HaveToken()
    {
        return true;
    }
}
