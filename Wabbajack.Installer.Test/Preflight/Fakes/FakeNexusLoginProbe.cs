using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

public sealed class FakeNexusLoginProbe : INexusLoginProbe
{
    public NexusLoginStatus Status { get; set; } = new(false, false, false, null, null);
    public int Calls { get; private set; }

    public Task<NexusLoginStatus> Probe(CancellationToken token)
    {
        Calls++;
        return Task.FromResult(Status);
    }
}
