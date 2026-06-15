using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.Downloaders.Dispatcher.Test;

public sealed class DispatcherClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection service)
    {
        service.AddOSIntegrated();
        service.AddSingleton<IUserInterventionHandler, CancellingInterventionHandler>();
    }

    private class CancellingInterventionHandler : IUserInterventionHandler
    {
        public void Raise(IUserIntervention intervention)
        {
            intervention.Cancel();
        }
    }
}
