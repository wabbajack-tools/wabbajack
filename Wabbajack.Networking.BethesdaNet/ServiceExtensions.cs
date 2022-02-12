using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack.Networking.BethesdaNet;

public static class ServiceExtensions
{
    public static void AddBethesdaNet(this IServiceCollection services)
    {
        services.AddSingleton<BethesdaNet.Client>();
    }
}