using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.DTOs.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
    }
}