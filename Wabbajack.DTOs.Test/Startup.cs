using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.DTOs.Test
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDTOConverters();
            services.AddDTOSerializer();
            services.AddWabbajackClient();
            services.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = 2});
            services.AddSingleton<HttpClient>();
        }
    }
}