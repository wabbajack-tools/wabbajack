using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.Installer.Test;

public sealed class InstallerClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection service)
    {
        service.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        service.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });
    }
}
