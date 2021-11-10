using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack.Networking.WabbajackClientApi;

public static class ServiceExtensions
{
    public static void AddWabbajackClient(this IServiceCollection services)
    {
        services.AddSingleton<Configuration>();
        services.AddSingleton<Client>();
    }
}