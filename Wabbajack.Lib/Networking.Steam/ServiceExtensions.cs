using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Networking.Steam;

namespace Wabbajack.Networking.NexusApi;

public static partial class ServiceExtensions
{
    public static void AddSteam(this IServiceCollection services)
    {
        services.AddSingleton<Client>();
        services.AddSingleton<DepotDownloader>();
    }
}