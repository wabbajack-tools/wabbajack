using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.RateLimiter;
using Wabbajack.Server.Lib.DTOs;
using Wabbajack.Server.Lib.TokenProviders;

namespace Wabbajack.Server.Lib;

public static class ServiceExtensions
{
    public static IServiceCollection AddServerLib(this IServiceCollection services)
    {
        return services
            .AddAllSingleton<ITokenProvider<Dictionary<StorageSpace, FtpSite>>, IFtpSiteCredentials,
                FtpSiteCredentialsProvider>()
            .AddSingleton<IResource<IFtpSiteCredentials>>(s => new Resource<IFtpSiteCredentials>("FTP Uploads", 8));
    }
}