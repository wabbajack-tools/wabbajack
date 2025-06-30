using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using System;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Http;

public static class ServiceExtensions
{
    public static void AddResumableHttpDownloader(this IServiceCollection services)
    {
        services.AddHttpClient("ResumableClient").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
        services.AddSingleton<IHttpDownloader, ResumableDownloader>();
        services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
    }
}