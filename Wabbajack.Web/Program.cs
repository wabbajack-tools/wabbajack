using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Wabbajack.Web.Services;

namespace Wabbajack.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddSingleton(_ => new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });
            builder.Services.AddMudServices();
            builder.Services.AddSingleton<StateContainer>();

            builder.Logging.SetMinimumLevel(builder.HostEnvironment.IsDevelopment()
                ? LogLevel.Debug
                : LogLevel.Information);

            await builder.Build().RunAsync();
        }
    }
}
