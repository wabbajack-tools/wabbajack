using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack.Networking.NexusApi;

public static partial class ServiceExtensions
{
    public static void AddNexusApi(this IServiceCollection services)
    {
        services.AddSingleton<NexusApi>();
        services.AddSingleton<ProxiedNexusApi>();
    }
}