using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.DTOs.Test;

public sealed class DtosClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
    }
}
