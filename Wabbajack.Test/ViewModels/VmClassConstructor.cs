using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Test.TestingInfra;

namespace Wabbajack.ViewModels.Test;

/// <summary>
/// DI for headless ViewModel tests: the offline OS-integrated services plus the application's own
/// ViewModel registrations (<see cref="ViewModelServiceExtensions.AddViewModels"/>), so ViewModels
/// resolve exactly as they do in the real app.
/// </summary>
public sealed class VmClassConstructor : DiClassConstructorBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        services.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });
        services.AddViewModels();
        services.AddTransient<ViewModelTestHarness>();
    }
}
