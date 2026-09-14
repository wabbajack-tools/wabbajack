using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

public sealed class FakeNexusLoginProbe : INexusLoginProbe
{
    public NexusLoginStatus Status { get; set; } =
        new(false, false, null, null, NexusCredentialSource.None);
    public int Calls { get; private set; }

    public Task<NexusLoginStatus> Probe(CancellationToken token)
    {
        Calls++;
        return Task.FromResult(Status);
    }
}
