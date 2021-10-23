using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Http;

public static class ServiceExtensions
{
    public static void AddHttpDownloader(this IServiceCollection services)
    {
        services.AddSingleton<IHttpDownloader, SingleThreadedDownloader>();
    }
}