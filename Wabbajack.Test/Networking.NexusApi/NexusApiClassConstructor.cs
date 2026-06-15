using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.Networking.NexusApi.Test;

public sealed class NexusApiClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection service)
    {
        service.AddOSIntegrated();
    }
}
